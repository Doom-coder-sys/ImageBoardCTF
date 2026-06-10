(() => {
    const input = document.getElementById('bodyInput');
    const preview = document.getElementById('livePreview');
    if (!input || !preview) return;

    const render = () => {
        preview.innerHTML = input.value || '<span class="muted">preview empty</span>';
    };

    input.addEventListener('input', render);
    render();
})();
