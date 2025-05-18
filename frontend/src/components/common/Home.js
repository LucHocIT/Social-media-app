import React, { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../services/AuthContext';
import LoginModal from '../auth/LoginModal';
import RegisterModal from '../auth/RegisterModal';

const Home = () => {
  const { isAuthenticated } = useAuth();
  const [isLoginModalOpen, setIsLoginModalOpen] = useState(false);
  const [isRegisterModalOpen, setIsRegisterModalOpen] = useState(false);
  const navigate = useNavigate();
  const [scrolled, setScrolled] = useState(false);
  const [animatedItems, setAnimatedItems] = useState({});

  // Handle scroll events for animations
  useEffect(() => {
    const handleScroll = () => {
      const scrollPosition = window.scrollY;
      
      // Navbar effect
      if (scrollPosition > 50) {
        setScrolled(true);
      } else {
        setScrolled(false);
      }

      // Animate elements when they come into view
      document.querySelectorAll('.animate-on-scroll:not(.animated)').forEach(item => {
        const rect = item.getBoundingClientRect();
        if (rect.top <= window.innerHeight * 0.8) {
          item.classList.add('animated');
          setAnimatedItems(prev => ({ ...prev, [item.dataset.id]: true }));
        }
      });
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial check
    
    return () => {
      window.removeEventListener('scroll', handleScroll);
    };
  }, []);

  // Toggle login modal
  const toggleLoginModal = () => {
    setIsLoginModalOpen(prev => !prev);
    if (isRegisterModalOpen) setIsRegisterModalOpen(false);
  };

  // Toggle register modal
  const toggleRegisterModal = () => {
    setIsRegisterModalOpen(prev => !prev);
    if (isLoginModalOpen) setIsLoginModalOpen(false);
  };

  // Switch from login to register
  const handleSwitchToRegister = () => {
    setIsLoginModalOpen(false);
    setTimeout(() => setIsRegisterModalOpen(true), 300);
  };

  // Switch from register to login
  const handleSwitchToLogin = () => {
    setIsRegisterModalOpen(false);
    setTimeout(() => setIsLoginModalOpen(true), 300);
  };

  return (
    <div className="home-container">
      <div className="hero-section">
        <div className="hero-background"></div>
        <div className="container">
          <div className="row align-items-center">
            <div className="col-lg-6 text-lg-start mb-5 mb-lg-0">
              <div className="hero-content animate-on-scroll fade-in-up" data-id="hero-title">
                <h1 className="hero-title">Connect with friends on <span className="text-primary">SocialApp</span></h1>
                <p className="hero-subtitle mt-4">
                  Share moments, discover content, and build meaningful connections in one place.
                </p>
                
                {isAuthenticated ? (
                  <div className="mt-4 animate-on-scroll fade-in-up" data-id="hero-cta">
                    <Link to="/profile" className="btn btn-primary btn-lg btn-hover-elevate rounded-pill px-5 py-3 me-3">
                      <i className="bi bi-person-circle me-2"></i>Go to Profile
                    </Link>
                  </div>
                ) : (
                  <div className="mt-5 d-flex flex-wrap gap-3 animate-on-scroll fade-in-up" data-id="hero-cta">
                    <button onClick={toggleRegisterModal} className="btn btn-primary btn-lg btn-hover-elevate rounded-pill px-4 py-3">
                      <i className="bi bi-person-plus me-2"></i>Join Now
                    </button>
                    <button onClick={toggleLoginModal} className="btn btn-outline-primary btn-lg btn-hover-elevate rounded-pill px-4 py-3">
                      <i className="bi bi-box-arrow-in-right me-2"></i>Login
                    </button>
                  </div>
                )}
              </div>
            </div>
            
            <div className="col-lg-6">
              <div className="hero-image animate-on-scroll fade-in" data-id="hero-image">
                <img src="https://placehold.co/600x500/e9ecef/6c757d?text=Social+App+Illustration" 
                     alt="SocialApp" 
                     className="img-fluid rounded-4 shadow-lg" />
                <div className="floating-card card-1">
                  <i className="bi bi-chat-dots-fill"></i>
                  <span>Connect</span>
                </div>
                <div className="floating-card card-2">
                  <i className="bi bi-share-fill"></i>
                  <span>Share</span>
                </div>
                <div className="floating-card card-3">
                  <i className="bi bi-heart-fill"></i>
                  <span>Engage</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>      <div className="features-section py-5">
        <div className="container">
          <h2 className="text-center mb-2 animate-on-scroll fade-in-up" data-id="features-heading">Why Choose SocialApp?</h2>
          <p className="text-center text-muted mb-5 animate-on-scroll fade-in-up" data-id="features-subheading">Experience a social network designed for modern connections</p>
          
          <div className="row g-4">
            <div className="col-md-4">
              <div className="feature-card animate-on-scroll fade-in-up" data-id="feature-1">
                <div className="feature-icon">
                  <i className="bi bi-people-fill"></i>
                </div>
                <h3 className="feature-title">Connect</h3>
                <p className="feature-text">
                  Connect with friends, family, and interesting people around the world. Build your network with meaningful relationships.
                </p>
              </div>
            </div>
            
            <div className="col-md-4">
              <div className="feature-card animate-on-scroll fade-in-up" data-id="feature-2">
                <div className="feature-icon">
                  <i className="bi bi-share-fill"></i>
                </div>
                <h3 className="feature-title">Share</h3>
                <p className="feature-text">
                  Share your thoughts, experiences, and meaningful moments with your network. Express yourself with rich media options.
                </p>
              </div>
            </div>
            
            <div className="col-md-4">
              <div className="feature-card animate-on-scroll fade-in-up" data-id="feature-3">
                <div className="feature-icon">
                  <i className="bi bi-compass-fill"></i>
                </div>
                <h3 className="feature-title">Discover</h3>
                <p className="feature-text">
                  Discover new content, ideas, and people based on your interests. Our smart algorithms help you find what matters most.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      <div className="stats-section py-5 bg-primary text-white">
        <div className="container">
          <div className="row g-4 justify-content-center">
            <div className="col-6 col-md-3">
              <div className="text-center animate-on-scroll fade-in" data-id="stat-1">
                <div className="stat-number">1M+</div>
                <div className="stat-label">Users</div>
              </div>
            </div>
            <div className="col-6 col-md-3">
              <div className="text-center animate-on-scroll fade-in" data-id="stat-2">
                <div className="stat-number">50M+</div>
                <div className="stat-label">Posts</div>
              </div>
            </div>
            <div className="col-6 col-md-3">
              <div className="text-center animate-on-scroll fade-in" data-id="stat-3">
                <div className="stat-number">100+</div>
                <div className="stat-label">Countries</div>
              </div>
            </div>
            <div className="col-6 col-md-3">
              <div className="text-center animate-on-scroll fade-in" data-id="stat-4">
                <div className="stat-number">4.8<i className="bi bi-star-fill ms-2 small"></i></div>
                <div className="stat-label">Rating</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="how-it-works py-5">
        <div className="container">
          <h2 className="text-center mb-5 animate-on-scroll fade-in" data-id="how-it-works-heading">How It Works</h2>
          
          <div className="row g-4 align-items-center">
            <div className="col-md-6">
              <div className="animate-on-scroll fade-in-right" data-id="how-step-1">
                <h3 className="mb-4"><span className="badge bg-primary me-2">1</span> Create Your Profile</h3>
                <p className="lead mb-4">Sign up in seconds and customize your profile with your interests, photos, and personal information.</p>
                <ul className="list-unstyled">
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Easy registration</li>
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Customizable profiles</li>
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Privacy controls</li>
                </ul>
              </div>
            </div>
            <div className="col-md-6 text-center">
              <img src="https://placehold.co/500x300/e9ecef/6c757d?text=Create+Profile" alt="Create Profile" 
                   className="img-fluid rounded-4 shadow-sm animate-on-scroll fade-in-left" data-id="how-img-1" />
            </div>
          </div>
          
          <div className="row g-4 align-items-center mt-5 flex-md-row-reverse">
            <div className="col-md-6">
              <div className="animate-on-scroll fade-in-left" data-id="how-step-2">
                <h3 className="mb-4"><span className="badge bg-primary me-2">2</span> Connect With Others</h3>
                <p className="lead mb-4">Find and connect with friends, family, or make new connections based on shared interests.</p>
                <ul className="list-unstyled">
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Smart suggestions</li>
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Friend requests</li>
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Group connections</li>
                </ul>
              </div>
            </div>
            <div className="col-md-6 text-center">
              <img src="https://placehold.co/500x300/e9ecef/6c757d?text=Connect+With+Friends" alt="Connect With Friends" 
                   className="img-fluid rounded-4 shadow-sm animate-on-scroll fade-in-right" data-id="how-img-2" />
            </div>
          </div>
          
          <div className="row g-4 align-items-center mt-5">
            <div className="col-md-6">
              <div className="animate-on-scroll fade-in-right" data-id="how-step-3">
                <h3 className="mb-4"><span className="badge bg-primary me-2">3</span> Share and Engage</h3>
                <p className="lead mb-4">Share moments, thoughts, and experiences with your network. Engage with content you care about.</p>
                <ul className="list-unstyled">
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Rich media sharing</li>
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Comments and reactions</li>
                  <li className="mb-2"><i className="bi bi-check-circle-fill text-success me-2"></i> Real-time notifications</li>
                </ul>
              </div>
            </div>
            <div className="col-md-6 text-center">
              <img src="https://placehold.co/500x300/e9ecef/6c757d?text=Share+Content" alt="Share Content" 
                   className="img-fluid rounded-4 shadow-sm animate-on-scroll fade-in-left" data-id="how-img-3" />
            </div>
          </div>
        </div>
      </div>
      
      <div className="cta-section py-5 bg-light">
        <div className="container text-center">
          <div className="row justify-content-center">
            <div className="col-lg-8">
              <h2 className="animate-on-scroll fade-in-up" data-id="cta-heading">Ready to Join Our Community?</h2>
              <p className="lead mb-4 animate-on-scroll fade-in-up" data-id="cta-text">
                Connect with friends, share your moments, and discover new content in one place.
              </p>
              <div className="mt-4 animate-on-scroll fade-in-up" data-id="cta-buttons">
                {isAuthenticated ? (
                  <Link to="/profile" className="btn btn-lg btn-primary btn-hover-elevate rounded-pill px-5 py-3">
                    <i className="bi bi-person-circle me-2"></i>Go to Profile
                  </Link>
                ) : (
                  <div className="d-flex justify-content-center gap-3 flex-wrap">
                    <button onClick={toggleRegisterModal} className="btn btn-lg btn-primary btn-hover-elevate rounded-pill px-5 py-3">
                      <i className="bi bi-person-plus me-2"></i>Get Started
                    </button>
                    <button onClick={toggleLoginModal} className="btn btn-lg btn-outline-primary btn-hover-elevate rounded-pill px-5 py-3">
                      <i className="bi bi-box-arrow-in-right me-2"></i>Login
                    </button>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Login and Register Modals */}
      <LoginModal 
        isOpen={isLoginModalOpen} 
        onClose={() => setIsLoginModalOpen(false)} 
        onSwitchToRegister={handleSwitchToRegister} 
      />
      
      <RegisterModal 
        isOpen={isRegisterModalOpen} 
        onClose={() => setIsRegisterModalOpen(false)} 
        onSwitchToLogin={handleSwitchToLogin} 
      />
    </div>
  );
};

export default Home;
