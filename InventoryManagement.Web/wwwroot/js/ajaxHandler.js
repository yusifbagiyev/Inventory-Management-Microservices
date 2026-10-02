window.AjaxHandler = (function () {
    'use strict';

    // One submitting flag per form, so a double click sends one request.
    const submissionStates = new WeakMap();
    function handleForm(formSelector, options) {
        const defaults = {
            validateBeforeSubmit: true,
            successMessage: 'Operation completed successfully',
            successRedirect: null,
            redirectDelay: 1500,
            resetFormOnSuccess: false,
            onSuccess: null,
            onError: null,
            onBeforeSubmit: null
        };

        const settings = { ...defaults, ...options };

        const $forms = $(formSelector);

        if (!$forms.length) {
            console.error('Form not found:', formSelector);
            return;
        }

        $forms.each(function () {
            const $individualForm = $(this);
            const formElement = this;


            if (!submissionStates.has(formElement)) {
                submissionStates.set(formElement, { isSubmitting: false });
            }

            // Calling handleForm twice on the same form must not stack handlers.
            $individualForm.off('submit.ajaxHandler');

            // Only buttons that belong to this form, not to a form inside it.
            const $submitBtnInThisForm = $individualForm.find('button[type="submit"]').filter(function () {
                return $(this).closest('form')[0] === formElement;
            });

            if (!$submitBtnInThisForm.length) {
                console.warn('No submit button found in form:', $individualForm);
                return;
            }

            const originalButtonHtml = $submitBtnInThisForm.html();
            const originalButtonDisabled = $submitBtnInThisForm.prop('disabled');

            // Swallow clicks while a submit is still in flight.
            $submitBtnInThisForm.off('click.preventDouble');
            $submitBtnInThisForm.on('click.preventDouble', function (e) {
                const formState = submissionStates.get(formElement);
                if (formState.isSubmitting) {
                    e.preventDefault();
                    e.stopPropagation();
                    e.stopImmediatePropagation();
                    console.log('Preventing duplicate submission via click handler');
                    return false;
                }
            });


            $individualForm.on('submit.ajaxHandler', function (e) {
                e.preventDefault();
                e.stopPropagation();

                const form = this;
                const formState = submissionStates.get(form);

                if (formState.isSubmitting) {
                    console.log('Form is already being submitted, ignoring duplicate submission');
                    return false;
                }

                // Lock before validating so a second click cannot slip in.
                formState.isSubmitting = true;
                const $currentSubmitBtn = $(form).find('button[type="submit"]').filter(function () {
                    return $(this).closest('form')[0] === form;
                });

                $currentSubmitBtn.prop('disabled', true)
                    .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');


                if (settings.validateBeforeSubmit) {
                    if (!form.checkValidity()) {
                        form.reportValidity();
                        formState.isSubmitting = false;
                        $currentSubmitBtn.prop('disabled', originalButtonDisabled).html(originalButtonHtml);
                        return false;
                    }

                    if ($.validator && !$(form).valid()) {
                        formState.isSubmitting = false;
                        $currentSubmitBtn.prop('disabled', originalButtonDisabled).html(originalButtonHtml);
                        return false;
                    }
                }

                if (settings.onBeforeSubmit) {
                    const shouldContinue = settings.onBeforeSubmit(form);
                    if (shouldContinue === false) {
                        formState.isSubmitting = false;
                        $currentSubmitBtn.prop('disabled', originalButtonDisabled).html(originalButtonHtml);
                        return false;
                    }
                }

                $currentSubmitBtn.prop('disabled', true)
                    .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');

                const formData = new FormData(form);

                const restoreButton = () => {
                    $currentSubmitBtn.prop('disabled', originalButtonDisabled)
                        .html(originalButtonHtml);
                    formState.isSubmitting = false;
                };

                $.ajax({
                    url: form.action || window.location.href,
                    type: form.method || 'POST',
                    data: formData,
                    processData: false,
                    contentType: false,
                    success: function (response, textStatus, xhr) {
                        const contentType = xhr.getResponseHeader('content-type') || '';

                        if (contentType.indexOf('text/html') > -1) {
                            handleHtmlResponse(response, form, settings);
                        } else {
                            handleSuccess(response, form, settings);
                        }
                        restoreButton();
                    },
                    error: function (xhr, status, error) {
                        restoreButton();
                        handleError(xhr, form, settings);
                    },
                    complete: function () {
                        // Safety net in case a handler above threw before the button came back.
                        setTimeout(() => {
                            restoreButton();
                        }, 3000);
                    }
                });

                return false;
            });
        });
    }

    function handleSuccess(response, form, settings) {
        // Check for an approval request before the error checks, which it could otherwise trip.
        if (isApprovalRequest(response)) {
            const message = response.message || 'Request submitted for approval';
            showToast(message, 'info');

            if (settings.successRedirect) {
                setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
            }
            return;
        }

        if (response && (
            response.isSuccess === false ||
            response.success === false ||
            (response.message && response.message.toLowerCase().includes('error'))
        )) {
            const errorMessage = response.message || 'Operation failed';
            showToast(errorMessage, 'error');

            if (settings.onError) {
                settings.onError(errorMessage, response);
            }
            return;
        }

        if (settings.onSuccess) {
            const result = settings.onSuccess(response);
            if (result === false) return;
        }

        showToast(settings.successMessage, 'success');

        if (settings.resetFormOnSuccess) {
            form.reset();
        }

        if (settings.successRedirect) {
            setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
        }
    }

    function handleError(xhr, form, settings) {
        let errorMessage = 'An error occurred';
        let validationErrors = null;

        try {
            if (xhr.responseJSON) {
                errorMessage = xhr.responseJSON.message ||
                    xhr.responseJSON.error ||
                    xhr.responseJSON.title ||
                    errorMessage;

                if (xhr.responseJSON.errors) {
                    validationErrors = xhr.responseJSON.errors;
                    displayValidationErrors(form, validationErrors);
                }
            } else if (xhr.responseText) {
                try {
                    const response = JSON.parse(xhr.responseText);
                    errorMessage = response.message || errorMessage;
                } catch (e) {
                    if (xhr.responseText.length < 500 && !xhr.responseText.includes('<')) {
                        errorMessage = xhr.responseText;
                    }
                }
            }

            if (xhr.status === 400) {
                errorMessage = errorMessage || 'Invalid request. Please check your input.';
            } else if (xhr.status === 401) {
                errorMessage = 'Session expired. Please login again.';
                setTimeout(() => window.location.href = '/Account/Login', 2000);
            } else if (xhr.status === 403) {
                errorMessage = 'You do not have permission to perform this action.';
            } else if (xhr.status === 409) {
                errorMessage = errorMessage || 'This item already exists.';
            } else if (xhr.status >= 500) {
                errorMessage = 'Server error occurred. Please try again later.';
            }
        } catch (e) {
            console.error('Error parsing error response:', e);
        }

        showToast(errorMessage, 'error');

        if (settings.onError) {
            settings.onError(errorMessage, xhr);
        }
    }

    function isApprovalRequest(response) {
        if (!response) return false;

        return response.isApprovalRequest === true ||
            response.status === 'PendingApproval' ||
            response.Status === 'PendingApproval' ||
            response.approvalRequestId != null ||
            response.ApprovalRequestId != null;
    }

    function displayValidationErrors(form, errors) {
        $(form).find('.field-validation-error').removeClass('field-validation-error');
        $(form).find('.validation-message').remove();

        if (typeof errors === 'object') {
            for (const field in errors) {
                const $field = $(form).find(`[name="${field}"]`);
                if ($field.length) {
                    $field.addClass('is-invalid');
                    const messages = Array.isArray(errors[field]) ?
                        errors[field] : [errors[field]];
                    const errorHtml = `<span class="text-danger validation-message">
                                        ${messages.join(', ')}</span>`;
                    $field.after(errorHtml);
                }
            }
        }
    }

    function handleHtmlResponse(html, form, settings) {
        // An HTML reply is the form rendered again by the server with its validation errors.
        const $container = $(form).closest('.card-body');
        if ($container.length) {
            $container.html(html);
            // The swapped-in form has no submit handler yet.
            const $newForm = $container.find('form');
            if ($newForm.length) {
                const formId = $newForm.attr('id');
                if (formId) {
                    AjaxHandler.handleForm('#' + formId, settings);
                } else {
                    const uniqueId = 'form-' + Date.now();
                    $newForm.attr('id', uniqueId);
                    AjaxHandler.handleForm('#' + uniqueId, settings);
                }
            }
        }
    }

    return {
        handleForm: handleForm
    };
})();

window.ErrorHandler = {
    parseErrorMessage: function (xhr, defaultMessage) {
        defaultMessage = defaultMessage || 'An error occurred';

        try {
            if (xhr.responseJSON) {
                return xhr.responseJSON.message ||
                    xhr.responseJSON.error ||
                    defaultMessage;
            } else if (xhr.responseText) {
                const response = JSON.parse(xhr.responseText);
                return response.message || defaultMessage;
            }
        } catch (e) {
            console.error('Error parsing response:', e);
        }

        return defaultMessage;
    }
};