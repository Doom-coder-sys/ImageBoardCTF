(() => {
    const canvas = document.getElementById('matrix-rain');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    const chars = '0123456789010101011001110001011100101010110011010110';
    const fontSize = 18;
    const speed = Number(canvas.dataset.speed || '0.34');
    let columns = 0;
    let drops = [];

    const resize = () => {
        const parent = canvas.parentElement || document.body;
        const rect = parent.getBoundingClientRect();
        canvas.width = Math.max(rect.width, window.innerWidth);
        canvas.height = Math.max(rect.height, window.innerHeight);
        columns = Math.ceil(canvas.width / fontSize);
        drops = Array.from({ length: columns }, () => Math.random() * canvas.height / fontSize);
    };

    const draw = () => {
        ctx.fillStyle = 'rgba(0, 5, 2, 0.105)';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = '#43ff64';
        ctx.font = `${fontSize}px ui-monospace, SFMono-Regular, Consolas, monospace`;

        for (let i = 0; i < drops.length; i++) {
            const text = chars[Math.floor(Math.random() * chars.length)];
            ctx.fillText(text, i * fontSize, drops[i] * fontSize);
            if (drops[i] * fontSize > canvas.height && Math.random() > 0.982) drops[i] = 0;
            drops[i] += speed;
        }

        requestAnimationFrame(draw);
    };

    resize();
    window.addEventListener('resize', resize);
    draw();

    const overlay = document.getElementById('matrix-entry-overlay');
    if (overlay) {
        const duration = Number(overlay.dataset.duration || '3200');
        window.setTimeout(() => overlay.classList.add('matrix-entry-overlay--hide'), duration);
        window.setTimeout(() => overlay.remove(), duration + 900);
        if (window.history?.replaceState) {
            const url = new URL(window.location.href);
            url.searchParams.delete('rain');
            window.history.replaceState({}, document.title, url.pathname + url.search + url.hash);
        }
    }
})();
