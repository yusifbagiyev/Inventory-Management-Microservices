// Keeps the pending approval badges in the header and sidebar up to date
let isLoadingApprovals = false;
async function loadPendingApprovalsCount() {
    if (isLoadingApprovals) {
        console.log('Already loading approvals, skipping duplicate call');
        return;
    }

    isLoadingApprovals = true;
    const apiUrl = AppConfig.buildApiUrl('approvalrequests?pageNumber=1&pageSize=1');

    setTimeout(async () => {
        try {
            // The token comes from the provider, never from the DOM
            const token = await SecureTokenProvider.getToken();

            $.ajax({
                url: apiUrl,
                type: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                },
                timeout: 10000,
                success: function (data) {
                    const count = data.totalCount || 0;
                    updatePendingApprovalsCount(count);
                    console.log('✅ Loaded approval count:', count);
                },
                error: function (xhr, status, error) {
                    console.error('❌ Failed to load pending approvals:', {
                        status: xhr.status,
                        error: error,
                        responseText: xhr.responseText
                    });

                    if (xhr.status === 401) {
                        console.warn('Authentication expired, user needs to login');
                    } else if (xhr.status === 403) {
                        console.warn('User does not have permission to view approvals');
                    } else if (xhr.status === 0 || status === 'timeout') {
                        console.error('Network error or timeout occurred');
                    } else {
                        if (AppConfig.environment === 'development') {
                            console.error('API URL:', apiUrl);
                            console.error('Response:', xhr.responseText);
                        }
                    }
                },
                complete: function () {
                    isLoadingApprovals = false;
                }
            });
        } catch (error) {
            console.error('Failed to get token:', error);
            isLoadingApprovals = false;

            // A failed token fetch usually means the session has ended
            if (error.message.includes('Unauthorized')) {
                window.location.href = '/Account/Login';
            }
        }
    }, 250);
}

function updatePendingApprovalsCount(count) {
    count = parseInt(count) || 0;

    const $headerBadge = $('#pendingApprovalsCount');
    if ($headerBadge.length) {
        if (count > 0) {
            $headerBadge.text(count > 99 ? '99+' : count).show();
        } else {
            $headerBadge.hide();
        }
    }

    const $sidebarBadge = $('#sidebarPendingCount');
    if ($sidebarBadge.length) {
        if (count > 0) {
            $sidebarBadge.text(count > 99 ? '99+' : count).show();
        } else {
            $sidebarBadge.hide();
        }
    }

    window.currentApprovalsCount = count;

    // Other scripts on the page listen for this event
    $(document).trigger('approvals:count-updated', [count]);
}

// Use this one from handlers that can fire many times in a row
function debouncedLoadPendingApprovalsCount() {
    if (window.approvalsLoadTimeout) {
        clearTimeout(window.approvalsLoadTimeout);
    }

    window.approvalsLoadTimeout = setTimeout(() => {
        loadPendingApprovalsCount();
    }, 500);
}

window.loadPendingApprovalsCount = loadPendingApprovalsCount;
window.debouncedLoadPendingApprovalsCount = debouncedLoadPendingApprovalsCount;
window.updatePendingApprovalsCount = updatePendingApprovalsCount;