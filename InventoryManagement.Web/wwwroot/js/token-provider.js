// Gets the API token from the server when needed. It lives only in memory, never in the DOM or localStorage.
window.SecureTokenProvider = (function () {
    'use strict';

    let cachedToken = null;
    let tokenExpiryTime = null;
    // Shared by concurrent callers so only one request goes to the server.
    let fetchPromise = null;

    async function getToken() {
        if (cachedToken && tokenExpiryTime && Date.now() < tokenExpiryTime) {
            console.log('Using cached token');
            return cachedToken;
        }

        if (fetchPromise) {
            console.log('Token fetch already in progress, waiting...');
            return await fetchPromise;
        }

        console.log('Fetching fresh token from server...');
        fetchPromise = fetchTokenFromServer();

        try {
            const token = await fetchPromise;
            return token;
        } finally {
            fetchPromise = null;
        }
    }

    async function fetchTokenFromServer() {
        try {
            const response = await fetch('/api/token/current', {
                method: 'GET',
                credentials: 'same-origin',
                headers: {
                    'Accept': 'application/json'
                }
            });

            if (!response.ok) {
                if (response.status === 401) {
                    console.error('Token fetch failed: Unauthorized');
                    window.location.href = '/Account/Login';
                    throw new Error('Unauthorized');
                }
                throw new Error(`Token fetch failed with status ${response.status}`);
            }

            const data = await response.json();

            if (!data.token) {
                throw new Error('No token in response');
            }

            // Short cache so a token refreshed on the server is picked up soon.
            cachedToken = data.token;
            tokenExpiryTime = Date.now() + (2 * 60 * 1000);

            console.log('Token fetched and cached successfully');
            return cachedToken;

        } catch (error) {
            console.error('Error fetching token:', error);
            cachedToken = null;
            tokenExpiryTime = null;
            throw error;
        }
    }

    // Call on logout or when the server rejects the token.
    function clearToken() {
        console.log('Clearing cached token');
        cachedToken = null;
        tokenExpiryTime = null;
    }

    async function refreshToken() {
        console.log('Forcing token refresh');
        clearToken();
        return await getToken();
    }

    async function isAuthenticated() {
        try {
            const response = await fetch('/api/token/validate', {
                method: 'GET',
                credentials: 'same-origin'
            });

            if (!response.ok) {
                return false;
            }

            const data = await response.json();
            return data.isValid === true;
        } catch (error) {
            console.error('Error checking authentication:', error);
            return false;
        }
    }

    return {
        getToken: getToken,
        clearToken: clearToken,
        refreshToken: refreshToken,
        isAuthenticated: isAuthenticated
    };
})();