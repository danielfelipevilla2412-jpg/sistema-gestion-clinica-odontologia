// Mobile menu toggle
const menuToggle = document.getElementById('menu-toggle');
const sidebar = document.getElementById('sidebar');
const overlay = document.getElementById('overlay');
const supportButton = document.querySelector('.btn-support');
const supportPanel = document.getElementById('support-panel');

if (supportButton && supportPanel) {
    supportButton.addEventListener('click', (e) => {
        e.preventDefault();
        const isOpen = supportPanel.classList.toggle('open');
        supportPanel.hidden = !isOpen;
        supportButton.setAttribute('aria-expanded', String(isOpen));
    });
}

if (menuToggle && sidebar && overlay) {
    menuToggle.addEventListener('click', () => {
        sidebar.classList.add('open');
        overlay.classList.add('active');
    });

    overlay.addEventListener('click', () => {
        sidebar.classList.remove('open');
        overlay.classList.remove('active');
    });
}

// Category Filtering
const categoryCards = document.querySelectorAll('.category-card');
const articleItems = document.querySelectorAll('.article-item');
const viewAllLink = document.querySelector('.view-all');

categoryCards.forEach(card => {
    card.addEventListener('click', () => {
        const category = card.getAttribute('data-category');
        const isActive = card.classList.contains('active');

        // Toggle active class on cards
        categoryCards.forEach(c => c.classList.remove('active'));
        
        if (isActive) {
            // If already active, deactivate and show all articles
            showAllArticles();
        } else {
            // Activate clicked card and filter articles
            card.classList.add('active');
            filterArticles(category);
        }
    });
});

if (viewAllLink) {
    viewAllLink.addEventListener('click', (e) => {
        e.preventDefault();
        categoryCards.forEach(c => c.classList.remove('active'));
        showAllArticles();
    });
}

function filterArticles(category) {
    articleItems.forEach(item => {
        if (item.getAttribute('data-category') === category) {
            item.style.display = 'flex';
        } else {
            item.style.display = 'none';
        }
    });
}

function showAllArticles() {
    articleItems.forEach(item => {
        item.style.display = 'flex';
    });
}

// Search bar filtering
const searchInput = document.querySelector('.search-input');
if (searchInput) {
    searchInput.addEventListener('input', (e) => {
        const query = e.target.value.toLowerCase();
        
        // Reset category active state when typing search query
        categoryCards.forEach(c => c.classList.remove('active'));
        
        articleItems.forEach(item => {
            const title = item.querySelector('h4').textContent.toLowerCase();
            const text = item.querySelector('p').textContent.toLowerCase();
            if (title.includes(query) || text.includes(query)) {
                item.style.display = 'flex';
            } else {
                item.style.display = 'none';
            }
        });
    });
}