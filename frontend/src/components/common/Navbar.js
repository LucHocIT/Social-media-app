import React, { useState, useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../services/AuthContext';
import LoginModal from '../auth/LoginModal';
import RegisterModal from '../auth/RegisterModal';

const Navbar = () => {
  const { isAuthenticated, currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [scrolled, setScrolled] = useState(false);
  const [isLoginModalOpen, setIsLoginModalOpen] = useState(false);
  const [isRegisterModalOpen, setIsRegisterModalOpen] = useState(false);
  
  // Handle scroll event for navbar appearance
  useEffect(() => {
    const handleScroll = () => {
      if (window.scrollY > 50) {
        setScrolled(true);
      } else {
        setScrolled(false);
      }
    };
    
    window.addEventListener('scroll', handleScroll);
    
    return () => {
      window.removeEventListener('scroll', handleScroll);
    };
  }, []);

  const handleLogout = async () => {
    await logout();
    navigate('/');
  };

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

  // Switch between modals
  const handleSwitchToRegister = () => {
    setIsLoginModalOpen(false);
    setTimeout(() => setIsRegisterModalOpen(true), 300);
  };

  const handleSwitchToLogin = () => {
    setIsRegisterModalOpen(false);
    setTimeout(() => setIsLoginModalOpen(true), 300);
  };

  return (
    <>
      <nav className={`navbar navbar-expand-lg navbar-dark ${scrolled ? 'navbar-scrolled' : 'bg-transparent'} sticky-top`}>
        <div className="container">
          <Link className="navbar-brand d-flex align-items-center" to="/">
            <i className="bi bi-people-fill me-2"></i>
            <span className="fw-bold">SocialApp</span>
          </Link>
          
          <button className="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav">
            <span className="navbar-toggler-icon"></span>
          </button>
          
          <div className="collapse navbar-collapse" id="navbarNav">
            <ul className="navbar-nav ms-auto align-items-center">
              <li className="nav-item">
                <Link className="nav-link" to="/"><i className="bi bi-house-door me-1"></i> Home</Link>
              </li>
              
              {isAuthenticated ? (
                <>
                  <li className="nav-item">
                    <Link className="nav-link" to="/profile"><i className="bi bi-person me-1"></i> Profile</Link>
                  </li>
                  <li className="nav-item">
                    <button 
                      className="nav-link btn btn-link" 
                      onClick={handleLogout}
                    >
                      <i className="bi bi-box-arrow-right me-1"></i> Logout
                    </button>
                  </li>
                  <li className="nav-item ms-2">
                    <div className="d-flex align-items-center">
                      <div className="avatar-sm me-2">
                        {currentUser?.profilePicture ? 
                          <img 
                            src={currentUser.profilePicture} 
                            alt={currentUser.username} 
                            className="avatar-sm rounded-circle"
                            style={{ width: '32px', height: '32px' }}
                          /> : 
                          <div className="avatar-placeholder rounded-circle bg-light text-primary d-flex align-items-center justify-content-center" style={{ width: '32px', height: '32px' }}>
                            {currentUser?.firstName?.charAt(0) || currentUser?.username?.charAt(0)}
                          </div>
                        }
                      </div>
                      <span className="nav-user-name">
                        {currentUser?.firstName || currentUser?.username}
                      </span>
                    </div>
                  </li>
                </>
              ) : (
                <>
                  <li className="nav-item">
                    <button className="nav-link btn-nav-link" onClick={toggleLoginModal}>
                      <i className="bi bi-box-arrow-in-right me-1"></i> Login
                    </button>
                  </li>
                  <li className="nav-item">
                    <button className="btn btn-outline-primary btn-sm rounded-pill ms-3 px-4" onClick={toggleRegisterModal}>
                      <i className="bi bi-person-plus me-1"></i> Sign Up
                    </button>
                  </li>
                </>
              )}
            </ul>
          </div>
        </div>
      </nav>

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
    </>
  );
};

export default Navbar;
