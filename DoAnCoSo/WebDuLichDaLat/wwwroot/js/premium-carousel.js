/**
 * Premium Carousel for Tourism Website
 * Features: 4.5 cards display, smooth transitions, touch support
 */

class PremiumCarousel {
    constructor(containerSelector, options = {}) {
        this.container = document.querySelector(containerSelector);
        if (!this.container) {
            console.error('Carousel container not found:', containerSelector);
            return;
        }

        // Default options
        this.options = {
            autoPlay: true,
            autoPlayInterval: 5000,
            showDots: true,
            showArrows: true,
            cardsToShow: 4.5,
            cardsToScroll: 1,
            gap: 24,
            responsive: {
                1200: { cardsToShow: 3.5 },
                992: { cardsToShow: 2.5 },
                768: { cardsToShow: 1.2 }
            },
            ...options
        };

        this.currentIndex = 0;
        this.isAnimating = false;
        this.autoPlayTimer = null;
        this.touchStartX = 0;
        this.touchEndX = 0;

        this.init();
    }

    init() {
        this.setupHTML();
        this.setupEventListeners();
        this.updateCardsToShow();
        this.updateCarousel();
        
        if (this.options.autoPlay) {
            this.startAutoPlay();
        }

        // Initialize with fade-in animation
        setTimeout(() => {
            this.container.classList.add('carousel-loaded');
        }, 100);
    }

    setupHTML() {
        const track = this.container.querySelector('.premium-carousel-track');
        const cards = Array.from(track.children);
        
        // Store original cards
        this.cards = cards;
        this.totalCards = cards.length;

        // Create navigation arrows
        if (this.options.showArrows) {
            this.createArrows();
        }

        // Create dots indicator
        if (this.options.showDots) {
            this.createDots();
        }

        // Add loading state
        this.container.classList.add('premium-carousel-loading');
        
        // Remove loading after setup
        setTimeout(() => {
            this.container.classList.remove('premium-carousel-loading');
        }, 500);
    }

    createArrows() {
        const prevBtn = document.createElement('button');
        prevBtn.className = 'premium-carousel-nav prev';
        prevBtn.innerHTML = '<i class="bi bi-chevron-left"></i>';
        prevBtn.setAttribute('aria-label', 'Previous slide');

        const nextBtn = document.createElement('button');
        nextBtn.className = 'premium-carousel-nav next';
        nextBtn.innerHTML = '<i class="bi bi-chevron-right"></i>';
        nextBtn.setAttribute('aria-label', 'Next slide');

        this.container.appendChild(prevBtn);
        this.container.appendChild(nextBtn);

        this.prevBtn = prevBtn;
        this.nextBtn = nextBtn;
    }

    createDots() {
        const dotsContainer = document.createElement('div');
        dotsContainer.className = 'premium-carousel-dots';

        const maxSlides = Math.ceil(this.totalCards - this.options.cardsToShow + 1);
        
        for (let i = 0; i < maxSlides; i++) {
            const dot = document.createElement('button');
            dot.className = 'premium-dot';
            dot.setAttribute('aria-label', `Go to slide ${i + 1}`);
            dot.addEventListener('click', () => this.goToSlide(i));
            dotsContainer.appendChild(dot);
        }

        this.container.parentNode.appendChild(dotsContainer);
        this.dotsContainer = dotsContainer;
        this.dots = Array.from(dotsContainer.children);
    }

    setupEventListeners() {
        // Arrow navigation
        if (this.prevBtn) {
            this.prevBtn.addEventListener('click', () => this.prevSlide());
        }
        if (this.nextBtn) {
            this.nextBtn.addEventListener('click', () => this.nextSlide());
        }

        // Keyboard navigation
        document.addEventListener('keydown', (e) => {
            if (this.container.matches(':hover')) {
                if (e.key === 'ArrowLeft') this.prevSlide();
                if (e.key === 'ArrowRight') this.nextSlide();
            }
        });

        // Touch/swipe support
        const track = this.container.querySelector('.premium-carousel-track');
        track.addEventListener('touchstart', (e) => this.handleTouchStart(e), { passive: true });
        track.addEventListener('touchmove', (e) => this.handleTouchMove(e), { passive: true });
        track.addEventListener('touchend', (e) => this.handleTouchEnd(e), { passive: true });

        // Mouse drag support (optional)
        let isDragging = false;
        let startX = 0;
        let scrollLeft = 0;

        track.addEventListener('mousedown', (e) => {
            isDragging = true;
            startX = e.pageX - track.offsetLeft;
            scrollLeft = track.scrollLeft;
            track.style.cursor = 'grabbing';
        });

        track.addEventListener('mouseleave', () => {
            isDragging = false;
            track.style.cursor = 'grab';
        });

        track.addEventListener('mouseup', () => {
            isDragging = false;
            track.style.cursor = 'grab';
        });

        track.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            e.preventDefault();
            const x = e.pageX - track.offsetLeft;
            const walk = (x - startX) * 2;
            track.scrollLeft = scrollLeft - walk;
        });

        // Pause autoplay on hover
        this.container.addEventListener('mouseenter', () => this.pauseAutoPlay());
        this.container.addEventListener('mouseleave', () => {
            if (this.options.autoPlay) this.startAutoPlay();
        });

        // Responsive handling
        window.addEventListener('resize', () => {
            clearTimeout(this.resizeTimer);
            this.resizeTimer = setTimeout(() => {
                this.updateCardsToShow();
                this.updateCarousel();
            }, 250);
        });

        // Intersection Observer for performance
        if ('IntersectionObserver' in window) {
            const observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        if (this.options.autoPlay) this.startAutoPlay();
                    } else {
                        this.pauseAutoPlay();
                    }
                });
            });
            observer.observe(this.container);
        }
    }

    updateCardsToShow() {
        const width = window.innerWidth;
        let cardsToShow = this.options.cardsToShow;

        // Apply responsive settings
        Object.keys(this.options.responsive).forEach(breakpoint => {
            if (width <= parseInt(breakpoint)) {
                cardsToShow = this.options.responsive[breakpoint].cardsToShow;
            }
        });

        this.currentCardsToShow = cardsToShow;
        this.maxIndex = Math.max(0, this.totalCards - Math.floor(cardsToShow));
    }

    updateCarousel() {
        if (this.isAnimating) return;

        const track = this.container.querySelector('.premium-carousel-track');
        const cardWidth = (100 / this.currentCardsToShow);
        const translateX = -(this.currentIndex * cardWidth);

        track.style.transform = `translateX(${translateX}%)`;

        // Update dots
        if (this.dots) {
            this.dots.forEach((dot, index) => {
                dot.classList.toggle('active', index === this.currentIndex);
            });
        }

        // Update arrow states
        if (this.prevBtn) {
            this.prevBtn.disabled = this.currentIndex === 0;
            this.prevBtn.style.opacity = this.currentIndex === 0 ? '0.5' : '1';
        }
        if (this.nextBtn) {
            this.nextBtn.disabled = this.currentIndex >= this.maxIndex;
            this.nextBtn.style.opacity = this.currentIndex >= this.maxIndex ? '0.5' : '1';
        }
    }

    nextSlide() {
        if (this.currentIndex < this.maxIndex) {
            this.goToSlide(this.currentIndex + this.options.cardsToScroll);
        } else if (this.options.loop) {
            this.goToSlide(0);
        }
    }

    prevSlide() {
        if (this.currentIndex > 0) {
            this.goToSlide(this.currentIndex - this.options.cardsToScroll);
        } else if (this.options.loop) {
            this.goToSlide(this.maxIndex);
        }
    }

    goToSlide(index) {
        if (this.isAnimating) return;

        this.currentIndex = Math.max(0, Math.min(index, this.maxIndex));
        this.isAnimating = true;

        this.updateCarousel();

        // Reset animation flag after transition
        setTimeout(() => {
            this.isAnimating = false;
        }, 600);

        // Restart autoplay
        if (this.options.autoPlay) {
            this.startAutoPlay();
        }
    }

    startAutoPlay() {
        this.pauseAutoPlay();
        this.autoPlayTimer = setInterval(() => {
            if (this.currentIndex >= this.maxIndex) {
                this.goToSlide(0);
            } else {
                this.nextSlide();
            }
        }, this.options.autoPlayInterval);
    }

    pauseAutoPlay() {
        if (this.autoPlayTimer) {
            clearInterval(this.autoPlayTimer);
            this.autoPlayTimer = null;
        }
    }

    // Touch handling
    handleTouchStart(e) {
        this.touchStartX = e.touches[0].clientX;
        this.pauseAutoPlay();
    }

    handleTouchMove(e) {
        this.touchEndX = e.touches[0].clientX;
    }

    handleTouchEnd(e) {
        const touchDiff = this.touchStartX - this.touchEndX;
        const minSwipeDistance = 50;

        if (Math.abs(touchDiff) > minSwipeDistance) {
            if (touchDiff > 0) {
                this.nextSlide();
            } else {
                this.prevSlide();
            }
        }

        if (this.options.autoPlay) {
            this.startAutoPlay();
        }
    }

    // Public methods
    destroy() {
        this.pauseAutoPlay();
        // Remove event listeners and clean up
        if (this.prevBtn) this.prevBtn.remove();
        if (this.nextBtn) this.nextBtn.remove();
        if (this.dotsContainer) this.dotsContainer.remove();
    }

    refresh() {
        this.updateCardsToShow();
        this.updateCarousel();
    }

    // Accessibility improvements
    addAriaLabels() {
        const track = this.container.querySelector('.premium-carousel-track');
        track.setAttribute('role', 'region');
        track.setAttribute('aria-label', 'Destination carousel');

        this.cards.forEach((card, index) => {
            card.setAttribute('aria-label', `Destination ${index + 1} of ${this.totalCards}`);
        });
    }
}

// Auto-initialize carousels when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    // Initialize main destinations carousel
    const destinationsCarousel = document.querySelector('.premium-carousel-container');
    if (destinationsCarousel) {
        new PremiumCarousel('.premium-carousel-container', {
            autoPlay: true,
            autoPlayInterval: 6000,
            showDots: true,
            showArrows: true,
            cardsToShow: 4.5,
            cardsToScroll: 1
        });
    }

    // Add smooth scroll behavior for better UX
    document.documentElement.style.scrollBehavior = 'smooth';
});

// Export for use in other scripts
window.PremiumCarousel = PremiumCarousel;