/*
 * Harry Potter Explorer - client behaviour.
 *
 * No framework and no build step: every page is server-rendered, and this file adds
 * the three things that genuinely need the browser - the private card collection in
 * localStorage, infinite scroll, and the filter bar. New cards arrive from the server
 * as HTML (see CharactersController.Cards), so there is no card template here to keep
 * in sync with the Razor one.
 */
(function () {
    "use strict";

    // ---------------------------------------------------------------- storage

    var STORAGE_KEY = "hpx.collection.v1";

    var collection = {
        ids: [],

        load: function () {
            try {
                var raw = window.localStorage.getItem(STORAGE_KEY);
                var parsed = raw ? JSON.parse(raw) : [];
                this.ids = Array.isArray(parsed) ? parsed.filter(function (x) { return typeof x === "string"; }) : [];
            } catch (err) {
                // Private mode, disabled storage, corrupted value - all mean "no collection".
                this.ids = [];
            }
            return this.ids;
        },

        save: function () {
            try {
                window.localStorage.setItem(STORAGE_KEY, JSON.stringify(this.ids));
            } catch (err) {
                /* Storage full or blocked: the in-memory copy still works for this visit. */
            }
        },

        has: function (id) { return this.ids.indexOf(id) !== -1; },

        toggle: function (id) {
            var index = this.ids.indexOf(id);
            if (index === -1) {
                this.ids.push(id);
            } else {
                this.ids.splice(index, 1);
            }
            this.save();
            return index === -1;
        },

        count: function () { return this.ids.length; }
    };

    collection.load();

    // ------------------------------------------------------------------ utils

    function $(selector, root) { return (root || document).querySelector(selector); }
    function $$(selector, root) {
        return Array.prototype.slice.call((root || document).querySelectorAll(selector));
    }

    function debounce(fn, wait) {
        var timer = null;
        return function () {
            var args = arguments, self = this;
            window.clearTimeout(timer);
            timer = window.setTimeout(function () { fn.apply(self, args); }, wait);
        };
    }

    function announce(message) {
        var region = $("[data-live-region]");
        if (region) { region.textContent = message; }
    }

    // ------------------------------------------------------- collection paint

    function updateCountPill() {
        $$("[data-collection-count]").forEach(function (pill) {
            var n = collection.count();
            pill.textContent = n > 99 ? "99+" : String(n);
            pill.setAttribute("data-count", String(n));
        });
    }

    function paintCollectButtons(root) {
        $$("[data-collect]", root).forEach(function (button) {
            var id = button.getAttribute("data-collect");
            var held = collection.has(id);
            var name = button.getAttribute("data-name") || "this character";

            button.setAttribute("aria-pressed", held ? "true" : "false");
            button.setAttribute("title", held ? "Remove from your collection" : "Add to your collection");
            button.setAttribute("aria-label", (held ? "Remove " : "Add ") + name +
                (held ? " from your collection" : " to your collection"));
        });
    }

    /**
     * Tells the server that *somebody* collected a card, so the public tally and the
     * live ledger stay honest. The list of ids itself never leaves the browser.
     */
    function reportToggle(id, collected) {
        return window.fetch("/api/collection/" + encodeURIComponent(id), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ collected: collected })
        }).catch(function () {
            /* Offline or the API is down: the local collection is still correct. */
        });
    }

    document.addEventListener("click", function (event) {
        var button = event.target.closest ? event.target.closest("[data-collect]") : null;
        if (!button) { return; }

        event.preventDefault();

        var id = button.getAttribute("data-collect");
        var name = button.getAttribute("data-name") || "Card";
        var nowHeld = collection.toggle(id);

        paintCollectButtons(document);
        updateCountPill();

        button.classList.remove("pop");
        void button.offsetWidth; // restart the animation
        button.classList.add("pop");

        announce(nowHeld ? name + " added to your collection." : name + " removed from your collection.");
        reportToggle(id, nowHeld);

        if (nowHeld === false && document.body.hasAttribute("data-collection-page")) {
            var card = button.closest(".char-card");
            if (card) { card.remove(); }
            renderCollectionSummary();
        }
    });

    // ---------------------------------------------------- broken portrait URLs

    // Some upstream image URLs 404. Swap in a placeholder rather than showing a torn icon.
    document.addEventListener("error", function (event) {
        var img = event.target;
        if (!img || img.tagName !== "IMG" || !img.classList.contains("portrait-img")) { return; }

        var holder = img.parentNode;
        img.remove();

        var fallback = document.createElement("div");
        fallback.className = "no-portrait";
        fallback.setAttribute("aria-hidden", "true");
        fallback.textContent = "🪄";
        holder.appendChild(fallback);
    }, true);

    // ------------------------------------------------------------- characters

    function initCatalog() {
        var grid = $("[data-character-grid]");
        if (!grid) { return; }

        var sentinel = $("[data-sentinel]");
        var status = $("[data-catalog-status]");
        var form = $("[data-catalog-filters]");
        var endpoint = grid.getAttribute("data-cards-endpoint") || "/characters/cards";

        var loadMore = $("[data-load-more]");

        var state = {
            page: parseInt(grid.getAttribute("data-page") || "1", 10),
            hasMore: grid.getAttribute("data-has-more") === "true",
            loading: false
        };

        /*
         * IntersectionObserver only fires when intersection *changes*. Replacing the grid
         * on a filter change can leave the sentinel continuously in view, in which case no
         * further callback ever arrives and the scroll silently stops loading. So after
         * every load we measure the sentinel ourselves and continue if it is still near
         * the viewport.
         */
        function continueIfSentinelVisible() {
            if (!sentinel || !state.hasMore || state.loading) { return; }
            if (sentinel.getBoundingClientRect().top < window.innerHeight + 600) {
                load(state.page + 1, false);
            }
        }

        function currentParams() {
            if (!form) { return new URLSearchParams(); }
            var params = new URLSearchParams();
            new FormData(form).forEach(function (value, key) {
                if (value !== null && String(value).length > 0) { params.set(key, String(value)); }
            });
            return params;
        }

        function showSkeletons(n) {
            var frag = document.createDocumentFragment();
            for (var i = 0; i < n; i++) {
                var s = document.createElement("div");
                s.className = "skeleton";
                s.setAttribute("data-skeleton", "");
                frag.appendChild(s);
            }
            grid.appendChild(frag);
        }

        function clearSkeletons() {
            $$("[data-skeleton]", grid).forEach(function (s) { s.remove(); });
        }

        function load(page, replace) {
            if (state.loading) { return; }
            state.loading = true;

            var params = currentParams();
            params.set("page", String(page));

            if (replace) { grid.innerHTML = ""; }
            showSkeletons(replace ? 8 : 4);

            window.fetch(endpoint + "?" + params.toString(), {
                headers: { "X-Requested-With": "fetch" }
            })
                .then(function (response) {
                    if (!response.ok) { throw new Error("Request failed: " + response.status); }
                    state.hasMore = response.headers.get("X-Has-More") === "true";
                    var total = response.headers.get("X-Total-Count");
                    if (status && total !== null) {
                        status.textContent = total === "1" ? "1 character" : total + " characters";
                    }
                    return response.text();
                })
                .then(function (html) {
                    clearSkeletons();
                    grid.insertAdjacentHTML("beforeend", html);
                    state.page = page;
                    paintCollectButtons(grid);

                    if (replace && grid.children.length === 0) {
                        grid.innerHTML =
                            '<div class="notice empty" style="grid-column:1/-1">' +
                            "Not a single portrait matches that. Try a different name or clear the filters." +
                            "</div>";
                    }

                    if (loadMore) { loadMore.hidden = !state.hasMore; }
                    window.setTimeout(continueIfSentinelVisible, 0);
                })
                .catch(function () {
                    clearSkeletons();
                    if (status) { status.textContent = "The owls could not reach the castle. Try again."; }
                })
                .then(function () {
                    state.loading = false;
                });
        }

        if (sentinel && "IntersectionObserver" in window) {
            var observer = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting && state.hasMore && !state.loading) {
                        load(state.page + 1, false);
                    }
                });
            }, { rootMargin: "600px 0px" });

            observer.observe(sentinel);
        }

        if (loadMore) {
            loadMore.addEventListener("click", function () {
                if (state.hasMore) { load(state.page + 1, false); }
            });
        }

        if (form) {
            var rerun = debounce(function () {
                var params = currentParams();
                var qs = params.toString();
                window.history.replaceState(null, "", qs ? "?" + qs : window.location.pathname);
                load(1, true);
            }, 280);

            form.addEventListener("input", rerun);
            form.addEventListener("change", rerun);
            form.addEventListener("submit", function (event) {
                event.preventDefault();
                rerun();
            });

            var reset = $("[data-reset-filters]", form);
            if (reset) {
                reset.addEventListener("click", function () {
                    form.reset();
                    $$("select", form).forEach(function (s) { s.value = ""; });
                    $$("input", form).forEach(function (i) { i.value = ""; });
                    rerun();
                });
            }
        }
    }

    // ----------------------------------------------------------- spell search

    function initSpells() {
        var list = $("[data-spell-list]");
        if (!list) { return; }

        var form = $("[data-spell-filters]");
        var status = $("[data-spell-status]");
        if (!form) { return; }

        var run = debounce(function () {
            var params = new URLSearchParams();
            new FormData(form).forEach(function (value, key) {
                if (value !== null && String(value).length > 0) { params.set(key, String(value)); }
            });

            var qs = params.toString();
            window.history.replaceState(null, "", qs ? "?" + qs : window.location.pathname);

            window.fetch("/spells/rows?" + qs, { headers: { "X-Requested-With": "fetch" } })
                .then(function (response) {
                    var total = response.headers.get("X-Total-Count");
                    if (status && total !== null) {
                        status.textContent = total === "1" ? "1 spell" : total + " spells";
                    }
                    return response.text();
                })
                .then(function (html) {
                    list.innerHTML = html.trim().length
                        ? html
                        : '<div class="notice empty">No spell by that name. Check your pronunciation.</div>';
                })
                .catch(function () {
                    if (status) { status.textContent = "Could not reach the library."; }
                });
        }, 250);

        form.addEventListener("input", run);
        form.addEventListener("change", run);
        form.addEventListener("submit", function (e) { e.preventDefault(); run(); });
    }

    // -------------------------------------------------------- artefact filter

    function initArtifacts() {
        var grid = $("[data-artifact-grid]");
        if (!grid) { return; }

        var search = $("[data-artifact-search]");
        var category = $("[data-artifact-category]");
        var status = $("[data-artifact-status]");

        // The whole vault is 24 items and already on the page, so filtering it in the
        // browser is instant and avoids a pointless round trip.
        function apply() {
            var term = (search && search.value || "").trim().toLowerCase();
            var cat = (category && category.value) || "";
            var shown = 0;

            $$("[data-artifact]", grid).forEach(function (card) {
                var haystack = card.getAttribute("data-search") || "";
                var cardCat = card.getAttribute("data-category") || "";
                var visible = (!term || haystack.indexOf(term) !== -1) && (!cat || cardCat === cat);
                card.hidden = !visible;
                if (visible) { shown++; }
            });

            if (status) {
                status.textContent = shown === 1 ? "1 artefact" : shown + " artefacts";
            }
        }

        if (search) { search.addEventListener("input", apply); }
        if (category) { category.addEventListener("change", apply); }
        apply();
    }

    // ------------------------------------------------------- collection page

    function renderCollectionSummary() {
        var summary = $("[data-collection-summary]");
        if (!summary) { return; }

        var n = collection.count();
        summary.textContent = n === 0
            ? "Your album is empty."
            : (n === 1 ? "1 card in your album." : n + " cards in your album.");

        var empty = $("[data-collection-empty]");
        if (empty) { empty.hidden = n !== 0; }
    }

    function initCollectionPage() {
        var grid = $("[data-collection-grid]");
        if (!grid) { return; }

        renderCollectionSummary();

        var ids = collection.load();
        if (ids.length === 0) { return; }

        grid.innerHTML = '<div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div>';

        window.fetch("/characters/cards/by-ids", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(ids)
        })
            .then(function (response) { return response.text(); })
            .then(function (html) {
                grid.innerHTML = html;
                paintCollectButtons(grid);

                // Ids that no longer resolve (renamed or removed upstream) are dropped,
                // so a stale album cannot keep asking for cards that do not exist.
                var alive = $$("[data-character-id]", grid).map(function (card) {
                    return card.getAttribute("data-character-id");
                });

                if (alive.length !== ids.length) {
                    collection.ids = collection.ids.filter(function (id) {
                        return alive.indexOf(id) !== -1;
                    });
                    collection.save();
                    updateCountPill();
                    renderCollectionSummary();
                }
            })
            .catch(function () {
                grid.innerHTML =
                    '<div class="notice warn" style="grid-column:1/-1">Could not load your album just now.</div>';
            });

        var clear = $("[data-clear-collection]");
        if (clear) {
            clear.addEventListener("click", function () {
                if (!window.confirm("Empty your whole album? This cannot be undone.")) { return; }

                var toRelease = collection.ids.slice();
                collection.ids = [];
                collection.save();
                updateCountPill();
                grid.innerHTML = "";
                renderCollectionSummary();

                toRelease.forEach(function (id) { reportToggle(id, false); });
            });
        }
    }

    // ----------------------------------------------------------- sorting hat

    function initSortingHat() {
        var form = $("[data-sorting-hat]");
        if (!form) { return; }

        var output = $("[data-sorting-result]");

        form.addEventListener("submit", function (event) {
            event.preventDefault();

            var answers = {};
            new FormData(form).forEach(function (value, key) { answers[key] = String(value); });

            if (Object.keys(answers).length < form.querySelectorAll(".question").length) {
                output.innerHTML = '<div class="notice warn">The Hat would like an answer to every question.</div>';
                output.scrollIntoView({ block: "nearest" });
                return;
            }

            var button = $("[data-sorting-submit]", form);
            if (button) { button.disabled = true; button.textContent = "The Hat is thinking..."; }

            window.fetch("/api/sorting-hat", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(answers)
            })
                .then(function (response) { return response.json(); })
                .then(function (verdict) {
                    try {
                        window.localStorage.setItem("hpx.house", verdict.houseSlug);
                    } catch (err) { /* ignore */ }

                    output.innerHTML =
                        '<div class="panel verdict" style="--h1:' + verdict.primaryColour + ';--h2:' + verdict.secondaryColour + '">' +
                        '<div class="crest">' + verdict.crest + "</div>" +
                        "<h2>" + verdict.verdict + "</h2>" +
                        '<p class="muted">' +
                        (verdict.runnerUp ? "The Hat also considered " + verdict.runnerUp + "." : "The Hat had no doubts.") +
                        "</p>" +
                        '<p><a class="btn btn-primary" href="/houses/' + verdict.houseSlug + '">Read about ' + verdict.houseName + "</a></p>" +
                        "</div>";

                    output.scrollIntoView({ behavior: "smooth", block: "center" });
                })
                .catch(function () {
                    output.innerHTML = '<div class="notice warn">The Hat has lost its voice. Try again in a moment.</div>';
                })
                .then(function () {
                    if (button) { button.disabled = false; button.textContent = "Put on the Hat"; }
                });
        });
    }

    // ------------------------------------------------------------- decoration

    function initCandles() {
        var host = $("[data-candles]");
        if (!host || window.matchMedia("(prefers-reduced-motion: reduce)").matches) { return; }

        for (var i = 0; i < 14; i++) {
            var candle = document.createElement("span");
            candle.className = "candle";
            candle.style.left = (4 + Math.random() * 92).toFixed(2) + "%";
            candle.style.top = (6 + Math.random() * 68).toFixed(2) + "%";
            candle.style.animationDelay = (Math.random() * 6).toFixed(2) + "s";
            candle.style.animationDuration = (5.5 + Math.random() * 4).toFixed(2) + "s";
            host.appendChild(candle);
        }
    }

    // -------------------------------------------------------------- bootstrap

    function ready(fn) {
        if (document.readyState !== "loading") { fn(); }
        else { document.addEventListener("DOMContentLoaded", fn); }
    }

    ready(function () {
        updateCountPill();
        paintCollectButtons(document);
        initCatalog();
        initSpells();
        initArtifacts();
        initCollectionPage();
        initSortingHat();
        initCandles();
    });

    // Exposed so the /live page script can reuse the same helpers.
    window.HPX = {
        collection: collection,
        paintCollectButtons: paintCollectButtons,
        updateCountPill: updateCountPill,
        $: $,
        $$: $$
    };
})();
