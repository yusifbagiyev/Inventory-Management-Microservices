window.NotificationManager = (function () {
    'use strict';

    let connection = null;
    let connectionRetryCount = 0;
    const maxRetries = 5;
    let connectionState = 'disconnected';
    let reconnectTimeout = null;
    let isInitialized = false;

    // Recent ids are remembered for a few seconds so a repeated notification is dropped.
    const recentNotifications = new Map();
    const DUPLICATE_CHECK_WINDOW = 5000;


   async function initialize(isAdmin) {
        if (isInitialized) {
            console.log('NotificationManager already initialized, skipping');
            return;
        }

       try {
           const isAuthenticated = await SecureTokenProvider.isAuthenticated();

           if (!isAuthenticated) {
               console.log('User not authenticated, skipping notification initialization');
               return;
           }

           isInitialized = true;

           window.isAdmin = isAdmin;

           console.log('Initializing notification system for ' + (isAdmin ? 'admin' : 'regular') + ' user');

           // The token is fetched later, by the connection's accessTokenFactory.
           establishConnection();

       } catch (error) {
           console.error('Failed to check authentication status:', error);
           return;
       }
   }


    function establishConnection() {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            console.log('Already connected to notification hub');
            return;
        }

        if (connection) {
            console.log('Cleaning up existing connection');
            connection.stop();
            connection = null;
        }

        const hubUrl = AppConfig.signalR.notificationHub;
        console.log('Connecting to notification hub at:', hubUrl);

        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: async () => {
                    try {
                        // The token lives only in memory. It is never written to the DOM.
                        const token = await SecureTokenProvider.getToken();

                        if (!token) {
                            throw new Error('No authentication token available');
                        }

                        console.log('Token provided to SignalR connection');
                        return token;
                    } catch (error) {
                        console.error('Failed to get token for SignalR:', error);
                        throw new Error('Authentication failed - please refresh the page');
                    }
                },
                transport: signalR.HttpTransportType.WebSockets |
                    signalR.HttpTransportType.ServerSentEvents |
                    signalR.HttpTransportType.LongPolling,
                withCredentials: true
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    if (retryContext.previousRetryCount >= maxRetries) {
                        return null;
                    }
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
                }
            })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Handlers must be registered before start, or the first server messages are lost.
        setupConnectionHandlers();
        setupMessageHandlers();

        startConnection();
    }



    function setupConnectionHandlers() {
        connection.onreconnecting((error) => {
            connectionState = 'reconnecting';
            console.warn('SignalR connection lost, attempting to reconnect...', error);
            showToast('Connection lost. Reconnecting...', 'warning');
        });

        connection.onreconnected((connectionId) => {
            connectionState = 'connected';
            connectionRetryCount = 0;
            console.log('SignalR reconnected successfully:', connectionId);
            showToast('Connection restored', 'success');

            // Give the server a moment after a reconnect before reloading.
            setTimeout(() => {
                loadRecentNotifications();
                loadNotificationCount();

                if (window.isAdmin) {
                    debouncedLoadPendingApprovalsCount();
                }
            }, 1000);
        });

        connection.onclose((error) => {
            connectionState = 'disconnected';
            console.error('SignalR connection closed:', error);

            // Automatic reconnect has given up by now, so retry by hand a few more times.
            if (connectionRetryCount < maxRetries) {
                reconnectTimeout = setTimeout(() => {
                    console.log(`Attempting manual reconnection... (${connectionRetryCount + 1}/${maxRetries})`);
                    connectionRetryCount++;
                    startConnection();
                }, 5000);
            } else {
                console.error('Maximum reconnection attempts exceeded');
                showToast('Unable to connect to notification service', 'error');
                // Allow a fresh round of retries after a minute.
                setTimeout(() => {
                    connectionRetryCount = 0;
                }, 60000);
            }
        });
    }



    function setupMessageHandlers() {
        connection.on("ConnectionEstablished", function (data) {
            console.log('✅ SignalR connection established:', data);
            connectionState = 'connected';
            connectionRetryCount = 0;

            // Kept on window for debugging from the console.
            window.notificationInfo = {
                userId: data.userId,
                userName: data.userName,
                userGroup: data.userGroup,
                roleGroups: data.roleGroups
            };

            console.log('Connected as:', data.userName, 'Groups:', [data.userGroup, ...data.roleGroups]);

            // Short delay so the bell and its dropdown are on the page first.
            setTimeout(() => {
                loadRecentNotifications();
                loadNotificationCount();
            }, 500);
        });

        connection.on("ReceiveNotification", function (notification) {
            console.log('📨 Notification received:', notification);

            if (isDuplicateNotification(notification)) {
                console.log('Duplicate notification detected, ignoring:', notification.id);
                return;
            }

            trackNotification(notification);

            handleIncomingNotification(notification);
        });

        // Sent on connect for what arrived while offline. Only the badge changes, no toast for each.
        connection.on("ReceivePendingNotification", function (notification) {
            console.log('📬 Pending notification received:', notification);

            window.incrementNotificationCount();
        });

        connection.on("PendingNotificationsComplete", function (data) {
            console.log(`📭 Received ${data.count} pending notifications`);

            setTimeout(() => {
                window.loadRecentNotifications();
                window.loadNotificationCount();
            }, 100);
        });

        connection.on("RefreshApprovals", function (data) {
            console.log('🔄 Refresh approvals signal received:', data);

            if (window.isAdmin) {
                // admin-approvals.js may not be loaded on every page.
                if (typeof debouncedLoadPendingApprovalsCount === 'function') {
                    debouncedLoadPendingApprovalsCount();
                } else if (typeof loadPendingApprovalsCount === 'function') {
                    loadPendingApprovalsCount();
                }

                if (window.location.pathname.includes('/Approvals')) {
                    clearTimeout(window.approvalsPageRefreshTimeout);
                    window.approvalsPageRefreshTimeout = setTimeout(() => {
                        if (typeof window.refreshApprovalsList === 'function') {
                            window.refreshApprovalsList();
                        }
                    }, 1000);
                }
            }
        });
    }



    function isDuplicateNotification(notification) {
        if (!notification || !notification.id) {
            return false;
        }

        const notificationKey = `${notification.id}-${notification.type}`;
        const now = Date.now();

        if (recentNotifications.has(notificationKey)) {
            const lastSeen = recentNotifications.get(notificationKey);
            if (now - lastSeen < DUPLICATE_CHECK_WINDOW) {
                return true;
            }
        }

        return false;
    }



    function trackNotification(notification) {
        if (!notification || !notification.id) {
            return;
        }

        const notificationKey = `${notification.id}-${notification.type}`;
        const now = Date.now();

        recentNotifications.set(notificationKey, now);

        // Past 100 entries, keep only the 50 newest so the map does not grow forever.
        if (recentNotifications.size > 100) {
            const entries = Array.from(recentNotifications.entries());
            entries.sort((a, b) => b[1] - a[1]);

            recentNotifications.clear();
            entries.slice(0, 50).forEach(([key, timestamp]) => {
                recentNotifications.set(key, timestamp);
            });
        }
    }



    function startConnection() {
        if (connectionState === 'connecting') {
            console.log('Connection already in progress');
            return;
        }

        connectionState = 'connecting';

        connection.start()
            .then(() => {
                connectionState = 'connected';
                connectionRetryCount = 0;
                console.log('✅ SignalR connected successfully');

                if (reconnectTimeout) {
                    clearTimeout(reconnectTimeout);
                    reconnectTimeout = null;
                }
            })
            .catch(err => {
                connectionState = 'disconnected';
                console.error('❌ SignalR connection failed:', err);

                // Retrying an auth error is pointless, the page has to be refreshed.
                if (connectionRetryCount < maxRetries && !isAuthError(err)) {
                    connectionRetryCount++;
                    const delay = Math.min(1000 * Math.pow(2, connectionRetryCount), 10000);
                    console.log(`Retrying connection in ${delay}ms... (${connectionRetryCount}/${maxRetries})`);
                    reconnectTimeout = setTimeout(() => startConnection(), delay);
                } else if (isAuthError(err)) {
                    console.error('Authentication error, user may need to login');
                    showToast('Authentication expired. Please refresh the page.', 'warning');
                } else {
                    console.error('Failed to establish SignalR connection after maximum retries');
                    showToast('Unable to connect to notification service', 'error');
                }
            });
    }



    function isAuthError(error) {
        const errorMessage = error.message || error.toString();
        return errorMessage.includes('401') ||
            errorMessage.includes('Unauthorized') ||
            errorMessage.includes('authentication') ||
            errorMessage.includes('token');
    }



    function handleIncomingNotification(notification) {
        if (shouldPlaySound()) {
            window.playNotificationSound();
        }

        const toastType = window.getNotificationType(notification.type);
        showToast(`${notification.title}: ${notification.message}`, toastType);

        window.incrementNotificationCount();

        // A burst of notifications reloads the dropdown list only once.
        clearTimeout(window.notificationListReloadTimeout);
        window.notificationListReloadTimeout = setTimeout(() => {
            window.loadRecentNotifications();
        }, 500);

        handleSpecialNotifications(notification);

        $(document).trigger('notification:received', [notification]);
    }



    function handleSpecialNotifications(notification) {
        if (notification.type === 'ApprovalRequest' && window.isAdmin) {
            if (typeof debouncedLoadPendingApprovalsCount === 'function') {
                debouncedLoadPendingApprovalsCount();
            }

            // Wait a bit so several requests arriving together refresh the table once.
            if (window.location.pathname.includes('/Approvals')) {
                clearTimeout(window.approvalsRefreshTimeout);
                window.approvalsRefreshTimeout = setTimeout(() => {
                    refreshApprovalsTable();
                }, 1500);
            }
        }
        else if (notification.type === 'ApprovalResponse') {
            if (window.location.pathname.includes('/MyRequests')) {
                clearTimeout(window.myRequestsRefreshTimeout);
                window.myRequestsRefreshTimeout = setTimeout(() => {
                    location.reload();
                }, 1500);
            }
        }
    }

    function refreshApprovalsTable() {
        console.log('🔄 Refreshing approvals table...');

        const table = $('#approvalsTable');
        if (table.length && $.fn.DataTable.isDataTable(table)) {
            showSubtleLoader();

            // Fetch the page again and swap in only the stats cards and the table.
            $.ajax({
                url: window.location.pathname,
                type: 'GET',
                success: function (html) {
                    const $newContent = $(html);
                    const $newTable = $newContent.find('#approvalsTable').closest('.card');
                    const $newStats = $newContent.find('.row.mb-4').first();

                    if ($newStats.length) {
                        $('.row.mb-4').first().replaceWith($newStats);
                    }

                    if ($newTable.length) {
                        $('#approvalsTable').closest('.card').replaceWith($newTable);

                        $('#approvalsTable').DataTable({
                            order: [[0, 'desc']],
                            pageLength: 25
                        });

                        // Summaries are built by page script, which does not run on swapped-in HTML.
                        $('.request-summary').each(function () {
                            const $this = $(this);
                            const actionData = $this.data('action-data');
                            const requestType = $this.closest('tr').find('.badge').first().text().trim();

                            try {
                                // getRequestSummary is defined on the Approvals page.
                                const summary = getRequestSummary(requestType, actionData);
                                $this.html(summary);
                            } catch (e) {
                                $this.html('<span class="text-danger">Error parsing data</span>');
                            }
                        });

                        hideSubtleLoader();

                        console.log('✅ Approvals table refreshed successfully');
                    } else {
                        console.warn('Could not find table in response, reloading page');
                        location.reload();
                    }
                },
                error: function (xhr, status, error) {
                    console.error('Failed to refresh approvals table:', error);
                    hideSubtleLoader();

                    showToast('New approvals available. Click to refresh.', 'warning', 10000)
                        .addEventListener('click', function () {
                            location.reload();
                        });
                }
            });
        } else {
            console.log('No DataTable found, reloading page');
            location.reload();
        }
    }



    function showSubtleLoader() {
        if (!$('.subtle-loader').length) {
            const loader = $(`
            <div class="subtle-loader" style="position: fixed; top: 70px; right: 20px; z-index: 9998; 
                        background: rgba(255, 255, 255, 0.95); padding: 10px 20px; border-radius: 8px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.15); display: flex; align-items: center; gap: 10px;">
                <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                <span class="text-muted" style="font-size: 0.9rem;">Updating...</span>
            </div>
        `);
            $('body').append(loader);
        }
    }



    function hideSubtleLoader() {
        $('.subtle-loader').fadeOut(300, function () {
            $(this).remove();
        });
    }

    // At most one sound every 2 seconds.
    let lastSoundPlayed = 0;
    function shouldPlaySound() {
        const now = Date.now();
        const timeSinceLastSound = now - lastSoundPlayed;

        if (timeSinceLastSound > 2000) {
            lastSoundPlayed = now;
            return true;
        }
        return false;
    }


    window.refreshApprovalsTable = refreshApprovalsTable;
    window.showSubtleLoader = showSubtleLoader;
    window.hideSubtleLoader = hideSubtleLoader;


    return {
        initialize: initialize,
        getConnection: () => connection,
        getConnectionState: () => connectionState,
        isConnected: () => connectionState === 'connected',
        reconnect: () => {
            if (connectionState !== 'connected' && connectionState !== 'connecting') {
                console.log('Manual reconnection requested');
                connectionRetryCount = 0;
                establishConnection();
            } else {
                console.log('Already connected or connecting');
            }
        },
        disconnect: () => {
            console.log('Manual disconnection requested');
            isInitialized = false;
            if (connection) {
                connection.stop();
            }
            if (reconnectTimeout) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }
        }
    };
})();