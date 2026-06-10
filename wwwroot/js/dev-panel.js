(() => {
    window.__matrixBoard = {
        build: 'code-rain-2026.06',
        auth: {
            cookie: 'matrix_access',
            alg: 'HS256',
            roleClaim: 'role',
            jwtSecret: 'matrix-dev-secret-2026'
        },
        api: {
            admin: '/api/admin/panel',
            users: '/api/admin/users',
            rotate: '/api/admin/rotate-cache',
            debug: '/api/debug/session',
            backup: '/backup/matrix-board-backup.zip',
            env: '/.env'
        }
    };
})();
