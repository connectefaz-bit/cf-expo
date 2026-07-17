// Clean GET forms — strip inputs that equal their default value before submit
// so the browser never appends e.g. ?search=&type=All&page=1&pageSize=50.
(function () {
    var DEFAULTS = { type: 'All', search: '', pageSize: '50', page: '1', handle: '' };

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form.classList.contains('cf-clean-form')) return;

        // For each named control, disable it if its value equals the default.
        // Disabled fields are excluded from the GET query string.
        Array.prototype.forEach.call(form.elements, function (el) {
            if (!el.name || el.disabled) return;
            var def = DEFAULTS[el.name];
            if (def !== undefined && el.value === def) {
                el.disabled = true;
                // Re-enable after navigation so the form is usable if the user
                // hits back (browser restores state but controls stay disabled).
                setTimeout(function () { el.disabled = false; }, 500);
            }
        });
    });
}());
// Floating tooltip — rendered as position:fixed so it is never clipped by
// any overflow:auto scroll container (the table wrapper, the sidebar, etc.)
// One shared overlay div is created once and reused for every cell hover.
(function () {
    var tip, arrow;

    function init() {
        tip = document.createElement('div');
        tip.id = 'cf-floating-tip';
        document.body.appendChild(tip);

        arrow = document.createElement('div');
        arrow.id = 'cf-floating-tip-arrow';
        document.body.appendChild(arrow);
    }

    var GAP = 8; // px between cell edge and tooltip

    function show(cell) {
        var tpl = cell.querySelector('.cf-tooltip');
        if (!tpl) return;

        tip.innerHTML = tpl.innerHTML;
        tip.style.visibility = 'hidden';
        tip.style.display = 'block';
        arrow.style.display = 'block';

        var r = cell.getBoundingClientRect();
        var tw = tip.offsetWidth;
        var th = tip.offsetHeight;
        var AH = 8; // arrow height

        var above = r.top >= th + AH + GAP + 20;

        // Vertical
        var tipTop = above ? (r.top - th - AH - GAP) : (r.bottom + AH + GAP);
        var arrowTop = above ? (r.top - AH - GAP) : (r.bottom + GAP);

        // Horizontal — centre on cell, clamp to viewport
        var tipLeft = r.left + r.width / 2 - tw / 2;
        tipLeft = Math.max(8, Math.min(tipLeft, window.innerWidth - tw - 8));

        var arrowLeft = r.left + r.width / 2;

        tip.style.top = tipTop + 'px';
        tip.style.left = tipLeft + 'px';
        tip.style.visibility = '';

        arrow.style.top = arrowTop + 'px';
        arrow.style.left = arrowLeft + 'px';
        arrow.classList.toggle('cf-arrow-down', !above);

        tip.style.opacity = '1';
        arrow.style.opacity = '1';
    }

    function hide() {
        if (!tip) return;
        tip.style.opacity = '0';
        arrow.style.opacity = '0';
    }

    document.addEventListener('mouseenter', function (e) {
        if (!tip) init();
        var cell = e.target && e.target.closest
            ? e.target.closest('.cf-cell-solved, .cf-cell-unsolved')
            : null;
        if (cell) show(cell);
        else hide();
    }, true);

    document.addEventListener('mouseleave', function (e) {
        var cell = e.target && e.target.closest
            ? e.target.closest('.cf-cell-solved, .cf-cell-unsolved')
            : null;
        if (cell) hide();
    }, true);
}());
