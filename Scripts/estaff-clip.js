/* ============================================================
   ESTAFF — CLIP picker, classification toggle, status dialog

   Progressive enhancement only: every control here wraps a real form
   element that already works on its own. If this file fails to load the
   pages still submit, they are just less pleasant.
============================================================ */
(function () {
    'use strict';

    // ════════════════════════════════════════════
    // CLIP ITEM PICKER
    // ════════════════════════════════════════════

    // Most urgent first. Matches ClipService.SortByExpiry on the server.
    var URGENCY_ORDER = ['expired', 'soon', 'active', 'none'];

    var URGENCY_LABEL = {
        expired: 'Expired — action overdue',
        soon:    'Expiring soon',
        active:  'Active',
        none:    'No expiry date'
    };

    var URGENCY_ICON = {
        expired: 'fa-triangle-exclamation',
        soon:    'fa-clock',
        active:  'fa-circle-check',
        none:    'fa-circle-minus'
    };

    function ClipPicker(root) {
        this.root      = root;
        this.native    = root.querySelector('.clip-picker-native');
        this.trigger   = root.querySelector('.clip-picker-trigger');
        this.panel     = root.querySelector('.clip-picker-panel');
        this.search    = root.querySelector('.clip-picker-search');
        this.plant     = root.querySelector('.clip-picker-plant');
        this.list      = root.querySelector('.clip-picker-list');
        this.clearBtn  = root.querySelector('.clip-picker-clear');

        if (!this.native || !this.trigger || !this.panel || !this.list) return;

        this.items       = readItems(root);
        this.activeIndex = -1;
        this.visible     = [];

        root.classList.add('is-enhanced');
        this.bind();
        this.buildPlantOptions();
        this.render();
        this.syncTrigger();
    }

    // The server renders the item list as JSON in a <script type="application/json">
    // sibling so the markup stays valid and nothing has to be parsed out of
    // data-attributes on every option.
    function readItems(root) {
        var node = root.querySelector('.clip-picker-data');
        if (!node) return [];
        try {
            return JSON.parse(node.textContent || node.innerText || '[]') || [];
        } catch (e) {
            return [];
        }
    }

    ClipPicker.prototype.bind = function () {
        var self = this;

        this.trigger.addEventListener('click', function (e) {
            e.preventDefault();
            self.toggle();
        });

        this.trigger.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                self.open();
            }
        });

        if (this.search) {
            this.search.addEventListener('input', function () {
                self.render();
            });
            this.search.addEventListener('keydown', function (e) {
                self.onListKey(e);
            });
        }

        if (this.plant) {
            this.plant.addEventListener('change', function () {
                self.render();
            });
        }

        this.list.addEventListener('keydown', function (e) {
            self.onListKey(e);
        });

        if (this.clearBtn) {
            this.clearBtn.addEventListener('click', function (e) {
                e.preventDefault();
                self.select('');
                self.close();
                self.trigger.focus();
            });
        }

        document.addEventListener('click', function (e) {
            if (!self.root.contains(e.target)) self.close();
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && self.root.classList.contains('is-open')) {
                self.close();
                self.trigger.focus();
            }
        });

        // Keeps the enhanced view honest if anything sets the select directly.
        this.native.addEventListener('change', function () {
            self.syncTrigger();
        });
    };

    ClipPicker.prototype.onListKey = function (e) {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            this.move(1);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            this.move(-1);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            var item = this.visible[this.activeIndex];
            if (item) {
                this.select(item.key);
                this.close();
                this.trigger.focus();
            }
        }
    };

    ClipPicker.prototype.move = function (delta) {
        if (!this.visible.length) return;

        this.activeIndex += delta;
        if (this.activeIndex < 0) this.activeIndex = this.visible.length - 1;
        if (this.activeIndex >= this.visible.length) this.activeIndex = 0;

        this.highlight();
    };

    ClipPicker.prototype.highlight = function () {
        var buttons = this.list.querySelectorAll('.clip-picker-option');

        for (var i = 0; i < buttons.length; i++) {
            var isActive = i === this.activeIndex;
            buttons[i].classList.toggle('is-active', isActive);
            if (isActive && buttons[i].scrollIntoView) {
                buttons[i].scrollIntoView({ block: 'nearest' });
            }
        }

        this.trigger.setAttribute('aria-activedescendant',
            this.activeIndex >= 0 && buttons[this.activeIndex]
                ? buttons[this.activeIndex].id
                : '');
    };

    ClipPicker.prototype.toggle = function () {
        if (this.root.classList.contains('is-open')) this.close();
        else this.open();
    };

    ClipPicker.prototype.open = function () {
        if (this.trigger.disabled) return;

        this.root.classList.add('is-open');
        this.trigger.setAttribute('aria-expanded', 'true');

        if (this.search) {
            this.search.value = '';
            this.render();
            this.search.focus();
        }
    };

    ClipPicker.prototype.close = function () {
        this.root.classList.remove('is-open');
        this.trigger.setAttribute('aria-expanded', 'false');
        this.activeIndex = -1;
    };

    ClipPicker.prototype.setItems = function (items) {
        this.items = items || [];

        // Rebuild the underlying <select> so a non-JS submit still posts a
        // valid key and any stale selection is dropped.
        var previous = this.native.value;
        while (this.native.options.length > 1) this.native.remove(1);

        for (var i = 0; i < this.items.length; i++) {
            var item = this.items[i];
            var opt = document.createElement('option');
            opt.value = item.key;
            opt.textContent = item.kind + ' · ' + item.title +
                ' — ' + item.status + ' · ' + item.expiryText;
            this.native.appendChild(opt);
        }

        this.native.value = this.hasKey(previous) ? previous : '';
        this.render();
        this.syncTrigger();
    };

    ClipPicker.prototype.hasKey = function (key) {
        if (!key) return false;
        for (var i = 0; i < this.items.length; i++) {
            if (this.items[i].key === key) return true;
        }
        return false;
    };

    ClipPicker.prototype.select = function (key) {
        this.native.value = key || '';

        // Let anything listening (e.g. validation) know it changed.
        if (typeof Event === 'function') {
            this.native.dispatchEvent(new Event('change', { bubbles: true }));
        } else {
            this.syncTrigger();
        }
    };

    ClipPicker.prototype.find = function (key) {
        for (var i = 0; i < this.items.length; i++) {
            if (this.items[i].key === key) return this.items[i];
        }
        return null;
    };

    ClipPicker.prototype.syncTrigger = function () {
        var title = this.trigger.querySelector('.clip-picker-trigger-title');
        var sub   = this.trigger.querySelector('.clip-picker-trigger-sub');
        var icon  = this.trigger.querySelector('.clip-picker-trigger-icon');
        var item  = this.find(this.native.value);

        this.trigger.disabled = this.items.length === 0;

        if (!item) {
            title.textContent = this.items.length
                ? 'No CLIP item attached — click to choose'
                : 'No CLIP records exist';
            title.classList.add('is-placeholder');
            sub.textContent = '';
            sub.style.color = '';
            if (icon) icon.className = 'clip-picker-trigger-icon fas fa-list-ul clip-picker-caret';
            return;
        }

        title.textContent = item.kind + ' · ' + item.title;
        title.classList.remove('is-placeholder');

        sub.textContent = item.status + ' · ' + item.expiryText +
            (item.plant ? ' · ' + item.plant : '');
        sub.style.color = urgencyColor(item.urgency);
    };

    function urgencyColor(urgency) {
        if (urgency === 'expired') return '#991B1B';
        if (urgency === 'soon')    return '#92400E';
        if (urgency === 'active')  return '#065F46';
        return '';
    }

    // The plants actually represented in the list, each with a count, so the
    // filter can never offer a plant with nothing under it.
    ClipPicker.prototype.buildPlantOptions = function () {
        if (!this.plant) return;

        var counts = {};
        var names  = {};

        this.items.forEach(function (item) {
            var id = String(item.plantId || '');
            if (!id) return;
            counts[id] = (counts[id] || 0) + 1;
            names[id]  = item.plant || ('Plant ' + id);
        });

        var ids = Object.keys(counts).sort(function (a, b) {
            return names[a].localeCompare(names[b]);
        });

        while (this.plant.options.length > 1) this.plant.remove(1);

        var self = this;
        ids.forEach(function (id) {
            var opt = document.createElement('option');
            opt.value = id;
            opt.textContent = names[id] + ' (' + counts[id] + ')';
            self.plant.appendChild(opt);
        });

        var first = this.plant.options[0];
        if (first) first.textContent = 'All plants (' + this.items.length + ')';
    };

    ClipPicker.prototype.render = function () {
        var term = (this.search && this.search.value || '')
            .trim().toLowerCase();

        var plantId = (this.plant && this.plant.value || '').trim();

        var matches = this.items.filter(function (item) {
            if (plantId && String(item.plantId) !== plantId) return false;
            if (!term) return true;
            return [item.title, item.subtitle, item.plant,
                    item.kind, item.status, item.processStatus]
                .join(' ').toLowerCase().indexOf(term) !== -1;
        });

        this.visible = [];
        this.list.innerHTML = '';
        this.activeIndex = -1;

        if (!matches.length) {
            var empty = document.createElement('li');
            empty.className = 'clip-picker-empty';

            if (!this.items.length) {
                empty.textContent =
                    'No COF or plant monitoring records exist in CLIP.';
            } else if (term && plantId) {
                empty.textContent = 'Nothing matches “' + this.search.value +
                    '” in this plant. Try All plants.';
            } else if (plantId) {
                empty.textContent = 'No CLIP records for this plant.';
            } else {
                empty.textContent =
                    'No CLIP items match “' + this.search.value + '”.';
            }

            this.list.appendChild(empty);
            return;
        }

        var selected = this.native.value;
        var self = this;
        var index = 0;

        URGENCY_ORDER.forEach(function (urgency) {
            var group = matches.filter(function (i) {
                return i.urgency === urgency;
            });
            if (!group.length) return;

            self.list.appendChild(buildGroupHeading(urgency, group.length));

            group.forEach(function (item) {
                var li = document.createElement('li');
                li.appendChild(
                    buildOption(item, index, item.key === selected, self));
                self.list.appendChild(li);
                self.visible.push(item);
                index++;
            });
        });
    };

    function buildGroupHeading(urgency, count) {
        var li = document.createElement('li');
        li.className = 'clip-picker-group urgency-' + urgency;
        li.setAttribute('role', 'presentation');

        var icon = document.createElement('i');
        icon.className = 'fas ' + URGENCY_ICON[urgency];
        li.appendChild(icon);

        li.appendChild(document.createTextNode(URGENCY_LABEL[urgency]));

        var badge = document.createElement('span');
        badge.className = 'clip-picker-group-count';
        badge.textContent = count;
        li.appendChild(badge);

        return li;
    }

    function buildOption(item, index, isSelected, picker) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.id = 'clip-opt-' + Math.random().toString(36).slice(2, 9);
        btn.className = 'clip-picker-option urgency-' + item.urgency +
            (isSelected ? ' is-selected' : '');
        btn.setAttribute('role', 'option');
        btn.setAttribute('aria-selected', isSelected ? 'true' : 'false');

        var iconWrap = document.createElement('span');
        iconWrap.className = 'clip-picker-option-icon';
        var icon = document.createElement('i');
        icon.className = 'fas ' + (item.icon || 'fa-file');
        iconWrap.appendChild(icon);
        btn.appendChild(iconWrap);

        var body = document.createElement('span');
        body.className = 'clip-picker-option-body';

        var title = document.createElement('span');
        title.className = 'clip-picker-option-title';
        title.textContent = item.title;
        body.appendChild(title);

        var meta = document.createElement('span');
        meta.className = 'clip-picker-option-meta';
        meta.textContent = [item.kindLabel, item.subtitle, item.plant,
                            item.processStatus]
            .filter(Boolean).join(' · ');
        body.appendChild(meta);

        btn.appendChild(body);

        var expiry = document.createElement('span');
        expiry.className = 'clip-picker-option-expiry';
        expiry.appendChild(document.createTextNode(item.expiryText));
        var date = document.createElement('small');
        date.textContent = item.expiryDate;
        expiry.appendChild(date);
        btn.appendChild(expiry);

        btn.addEventListener('click', function (e) {
            e.preventDefault();
            picker.select(item.key);
            picker.close();
            picker.trigger.focus();
        });

        btn.addEventListener('mouseenter', function () {
            picker.activeIndex = index;
            picker.highlight();
        });

        return btn;
    }

    // ════════════════════════════════════════════
    // CLASSIFICATION → task type
    // ════════════════════════════════════════════
    //
    // Choosing a classification narrows the task-type list to that
    // classification's own rows. That is all it does.
    //
    // It used to do more: choosing the classification named "CLIP" hid the
    // task type, revealed the CLIP picker, and cleared any picked record when
    // you moved away again. The CLIP attachment is now independent of the
    // classification, so the picker stays visible and a record you have chosen
    // survives reclassifying the task — which is the point of the change.

    function wireClassificationField(field) {
        var radios    = field.querySelectorAll('.js-classification-radio');
        var listInput = field.querySelector('#TaskListId');

        if (!radios.length || !listInput) return;

        var allLists = readJson(field.querySelector('.task-list-data'), []);
        var selectedList = readJson(field.querySelector('.task-list-selected'), null);

        function selected() {
            var checked = field.querySelector('.js-classification-radio:checked');
            return checked ? checked.value : '';
        }

        function apply() {
            var current = selected();

            // Rebuild the task-type list for this classification, keeping the
            // current choice only if it still belongs.
            var previous = listInput.value || (selectedList !== null
                ? String(selectedList) : '');

            while (listInput.options.length > 1) listInput.remove(1);

            var matches = allLists.filter(function (l) {
                return String(l.classification) === current;
            });

            matches.forEach(function (l) {
                var opt = document.createElement('option');
                opt.value = l.id;
                opt.textContent = l.name;
                listInput.appendChild(opt);
            });

            listInput.value =
                matches.some(function (l) { return String(l.id) === previous; })
                    ? previous
                    : '';

            listInput.disabled = matches.length === 0;
        }

        for (var i = 0; i < radios.length; i++) {
            radios[i].addEventListener('change', apply);
        }

        apply();
    }

    function readJson(node, fallback) {
        if (!node) return fallback;
        try {
            var parsed = JSON.parse(node.textContent || node.innerText || 'null');
            return parsed === null ? fallback : parsed;
        } catch (e) {
            return fallback;
        }
    }

    // The picker used to refetch its list over XHR whenever the assignee
    // changed, because the options were the assignee's plants. It now carries
    // every CLIP record and filters by plant on the client, so there is nothing
    // to refetch and the list is never empty while an assignee is unchosen.

    // ════════════════════════════════════════════
    // SCHEDULE TYPE — daily or long term
    // ════════════════════════════════════════════

    // Swaps the date fields for the kind of task chosen: a daily task is
    // scheduled as the day and hours the work is done, a long-term task as a
    // due date, and the two share one slot in _ScheduleFields.
    //
    // The state the form opens in is rendered server-side; this takes over
    // from the first click. The rules behind it are the server's too -
    // TaskPeriod.Validate insists on a daily task's period, and
    // TaskPeriod.ApplyTo derives its due date from the period date and clears
    // a long-term task's hours. So an old copy of this file cached in a
    // browser costs the user the switching, not a wrong task.
    function wireScheduleChoice(group) {
        var form = group.form || group.closest('form');
        if (!form) return;

        var radios = form.querySelectorAll('input[name="ScheduleType"]');
        if (!radios.length) return;

        var periodDate = form.querySelector('[name="PeriodDate"]');
        var periodFields = [];
        var names = ['PeriodDate', 'PeriodStart', 'PeriodEnd'];

        for (var i = 0; i < names.length; i++) {
            var el = form.querySelector('[name="' + names[i] + '"]');
            if (el) periodFields.push(el);
        }

        if (!periodFields.length) return;

        var dueDate = form.querySelector('[name="DueDate"]');

        // The whole column, not the input: hiding the input alone would leave
        // its label and validation message behind, labelling nothing.
        function column(field) {
            return (field.closest && field.closest('[class*="col-"]'))
                || field.parentNode;
        }

        function show(field, visible) {
            var col = column(field);
            if (col) col.style.display = visible ? '' : 'none';
        }

        function selectedValue() {
            for (var i = 0; i < radios.length; i++) {
                if (radios[i].checked) return radios[i].value;
            }
            return null;
        }

        // What the user last put in the due date box themselves, so that
        // switching back to long term returns it rather than handing them the
        // period date the mirror below wrote while the box was out of sight.
        var typedDueDate = dueDate ? dueDate.value : '';

        // The due date box is hidden rather than removed or disabled, so it
        // still posts. What it posts is the period date, which is what the
        // server derives anyway - so a daily task reads the same whether or
        // not this ran.
        function mirrorDueDate() {
            if (!dueDate || !periodDate || !periodDate.value) return;
            dueDate.value = periodDate.value;
        }

        // A daily task is nearly always today's work, so the period date
        // opens on today rather than empty. Only when it is empty: an edit
        // form showing work done last week keeps last week, and a date the
        // user has already chosen is theirs.
        function defaultPeriodDate() {
            if (!periodDate || periodDate.value) return;

            var now = new Date();
            periodDate.value = now.getFullYear()
                + '-' + ('0' + (now.getMonth() + 1)).slice(-2)
                + '-' + ('0' + now.getDate()).slice(-2);
        }

        function sync() {
            // "1" is TaskScheduleType.Daily. Kept as the posted string rather
            // than mirrored into JS, so the two cannot drift apart.
            var daily = selectedValue() === '1';

            for (var i = 0; i < periodFields.length; i++) {
                show(periodFields[i], daily);
            }

            if (dueDate) show(dueDate, !daily);

            if (daily) {
                defaultPeriodDate();
                mirrorDueDate();
            } else if (dueDate) {
                dueDate.value = typedDueDate;
            }
        }

        for (var j = 0; j < radios.length; j++) {
            radios[j].addEventListener('change', sync);
        }

        if (dueDate) {
            // Only fires for a date the user chose: the mirror sets the value
            // directly, which raises no event.
            dueDate.addEventListener('change', function () {
                typedDueDate = dueDate.value;
            });
        }

        if (periodDate) {
            periodDate.addEventListener('change', sync);
            periodDate.addEventListener('input', sync);
        }

        sync();
    }

    // ════════════════════════════════════════════
    // BOOT
    // ════════════════════════════════════════════

    function init() {
        var pickers = document.querySelectorAll('.clip-picker');

        for (var i = 0; i < pickers.length; i++) {
            var picker = new ClipPicker(pickers[i]);
            if (!picker.native) continue;

            picker.native.clipPicker = picker;
        }

        var fields = document.querySelectorAll('.classification-field');
        for (var j = 0; j < fields.length; j++) wireClassificationField(fields[j]);

        var choices = document.querySelectorAll('.task-schedule-choice');
        for (var k = 0; k < choices.length; k++) wireScheduleChoice(choices[k]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
