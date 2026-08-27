// Task detail modal, shared by the admin and employee calendars.
//
// The panel is already on the page, rendered hidden by Razor as one block per
// item; the modal only borrows its markup. A copy rather than a move, so the
// same item can be opened again, and so the click stays instant and offline.
//
// Any element with data-detail="<panel id>" is a trigger, which lets each
// calendar keep its own item markup — the admin's chips are buttons, the
// employee's are cards, week links and month chips.
(function () {
    'use strict';

    var modalEl = document.getElementById('taskDetailModal');
    if (!modalEl || typeof bootstrap === 'undefined') { return; }

    var body  = document.getElementById('taskDetailModalBody');
    var modal = new bootstrap.Modal(modalEl);

    function open(trigger) {
        var panel = document.getElementById(
            trigger.getAttribute('data-detail'));
        if (!panel) { return; }

        body.innerHTML = panel.innerHTML;

        // The panel carries the plant's palette class; the body needs it too
        // or the stripe inside the modal has no colour to read.
        body.className = 'modal-body ' + panel.className
            .split(/\s+/)
            .filter(function (c) { return c && c !== 'tdp'; })
            .join(' ');

        modal.show();
    }

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-detail]');
        if (!trigger) { return; }

        // A control inside the item — complete, reopen, edit — does its own
        // job and must not also open the panel.
        var inner = e.target.closest('a, button');
        if (inner && inner !== trigger) { return; }

        // Some triggers are still anchors so they keep the styling their
        // calendar already gives them.
        if (trigger.tagName === 'A') { e.preventDefault(); }

        open(trigger);
    });

    // Triggers that are not buttons still answer the keyboard.
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') { return; }
        if (!e.target.closest) { return; }

        var trigger = e.target.closest('[data-detail]');
        if (!trigger || trigger !== e.target) { return; }
        if (trigger.tagName === 'BUTTON') { return; }

        e.preventDefault();
        open(trigger);
    });
})();
