document.addEventListener('click', function (event) {
    const stepButton = event.target.closest('[data-quantity-step]');
    if (stepButton) {
        const input = stepButton.closest('[data-quantity-control]').querySelector('input[type="number"]');
        const min = Number(input.min || 1);
        const max = Number(input.max || 10000);
        const value = Number.isFinite(input.valueAsNumber) ? input.valueAsNumber : min;
        input.value = Math.min(max, Math.max(min, value + Number(stepButton.dataset.quantityStep)));
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }
    if (event.target.closest('[data-guest-cart]')) {
        if (showAuthModal()) event.preventDefault();
    }
});

document.addEventListener('submit', function (event) {
    if (event.target.matches('[data-cart-form][data-guest="true"]')) {
        if (showAuthModal()) event.preventDefault();
    }
});

function showAuthModal() {
    const element = document.getElementById('authRequiredModal');
    if (!element || !window.bootstrap?.Modal) return false;
    window.bootstrap.Modal.getOrCreateInstance(element).show();
    return true;
}
