window.AppConfig = (function () {
    'use strict';

    const hostname = window.location.hostname;
    const protocol = window.location.protocol;
    const isProduction = hostname.includes('inventory166.az');
    const isDevelopment = !isProduction;

    const config = {
        environment: isDevelopment ? 'development' : 'production',
        hostname: hostname,
        protocol: protocol,

        api: {
            baseUrl: isDevelopment ? 'http://localhost:5000' : '',
            gateway: isDevelopment ? 'http://localhost:5000' : '/api',
        },

        signalR: {
            notificationHub: isDevelopment
                ? 'http://localhost:5005/notificationHub'
                : '/notificationHub',
            options: {
                skipNegotiation: false,
                transport: typeof signalR !== 'undefined' ?
                    (signalR.HttpTransportType.WebSockets |
                        signalR.HttpTransportType.ServerSentEvents |
                        signalR.HttpTransportType.LongPolling) : 1,
                withCredentials: true
            }
        },

        images: {
            products: isDevelopment
                ? 'http://localhost:5001/images/products'
                : '/images/products',
            routes: isDevelopment
                ? 'http://localhost:5002/images/routes'
                : '/images/routes'
        },
    };

    config.buildApiUrl = function (endpoint) {
        endpoint = endpoint.replace(/^\//, '');
        if (isProduction) {
            return `/api/${endpoint}`;
        }
        return `${this.api.gateway}/api/${endpoint}`;
    };
    return config;
})();