document.addEventListener("DOMContentLoaded", () => {
    const header = document.getElementById("site-header");
    if (header) {
        const updateHeader = () => {
            header.classList.toggle("shadow-md", window.scrollY > 10);
        };

        updateHeader();
        window.addEventListener("scroll", updateHeader, { passive: true });
    }

    document.querySelectorAll('a[href^="#"]').forEach((anchor) => {
        const href = anchor.getAttribute("href");
        if (!href || href.length < 2) {
            return;
        }

        const target = document.getElementById(href.slice(1));
        if (!target) {
            return;
        }

        anchor.addEventListener("click", (event) => {
            event.preventDefault();
            target.scrollIntoView({
                behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches
                    ? "auto"
                    : "smooth",
                block: "start"
            });
            document.querySelector(".public-mobile-menu[open]")?.removeAttribute("open");
        });
    });
});
