// Shell behaviour: the design system's mobile navigation drawer plus the inline alert
// dismiss control. Both are plain DOM, so the screens stay server-rendered and no view
// carries its own script.
(function () {
    "use strict";

    var sidebar = document.getElementById("sidebar");
    var menuButton = document.getElementById("menu-button");
    var navScrim = document.getElementById("nav-scrim");

    // Below 1024px the viewport media query switches .sidebar to a fixed drawer. The drawer is
    // inert while closed, so its links are not reachable by tab or screen reader until the Menu
    // control opens it - the export's own rule, not an addition.
    function setMobileNavigation(open, returnFocus) {
        if (!sidebar || !menuButton || !navScrim) {
            return;
        }

        var mobile = window.matchMedia("(max-width: 1023px)").matches;
        var shouldOpen = mobile && Boolean(open);

        document.body.classList.toggle("nav-open", shouldOpen);
        navScrim.hidden = !shouldOpen;
        menuButton.setAttribute("aria-expanded", String(shouldOpen));

        if (mobile && !shouldOpen) {
            sidebar.setAttribute("inert", "");
        } else {
            sidebar.removeAttribute("inert");
        }

        if (shouldOpen) {
            sidebar.querySelector(".nav-item.is-active")?.focus();
        } else if (returnFocus && mobile && sidebar.contains(document.activeElement)) {
            menuButton.focus();
        }
    }

    if (sidebar && menuButton && navScrim) {
        menuButton.addEventListener("click", function () {
            setMobileNavigation(menuButton.getAttribute("aria-expanded") !== "true", true);
        });

        navScrim.addEventListener("click", function () {
            setMobileNavigation(false, true);
        });

        window.addEventListener("resize", function () {
            setMobileNavigation(false, false);
        });

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && document.body.classList.contains("nav-open")) {
                setMobileNavigation(false, true);
            }
        });

        // A drawer link navigates, so the drawer is closed on the way out; leaving it open
        // would animate over the new page on a same-origin back navigation.
        sidebar.querySelectorAll(".nav-item.nav-link").forEach(function (link) {
            link.addEventListener("click", function () {
                setMobileNavigation(false, false);
            });
        });

        setMobileNavigation(false, false);
    }

    document.addEventListener("click", function (event) {
        var dismiss = event.target.closest("[data-dismiss-alert]");
        if (!dismiss) {
            return;
        }

        var alert = dismiss.closest(".alert");
        if (alert) {
            alert.remove();
        }
    });
})();
