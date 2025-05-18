import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../services/AuthContext';
import LoginForm from '../auth/LoginForm';
import RegisterForm from '../auth/RegisterForm';

const Home = () => {
  const { isAuthenticated } = useAuth();
  const [activeTab, setActiveTab] = useState('welcome');
  const navigate = useNavigate();

  // Handle successful authentication
  const handleAuthSuccess = () => {
    // Redirect to profile after successful authentication
    navigate('/profile');
  };

  return (
    <div className="home-container">
      <div className="hero-section text-center">
        <div className="container">
          <div className="row align-items-center">
            <div className="col-lg-6 text-lg-start mb-5 mb-lg-0">
              <h1 className="hero-title">Connect with friends on <span className="text-primary">SocialApp</span></h1>
              <p className="hero-subtitle mt-3">
                Share moments, discover content, and build meaningful connections in one place.
              </p>
              
              {isAuthenticated ? (
                <div className="mt-4">
                  <Link to="/profile" className="btn btn-primary btn-lg rounded-pill px-4 me-3">
                    <i className="bi bi-person-circle me-2"></i>Go to Profile
                  </Link>
                </div>
              ) : (
                <div className="auth-nav mt-4">
                  <ul className="nav nav-pills auth-tabs justify-content-center justify-content-lg-start" role="tablist">
                    <li className="nav-item">
                      <button 
                        className={`nav-link ${activeTab === 'welcome' ? 'active' : ''}`}
                        onClick={() => setActiveTab('welcome')}
                      >
                        <i className="bi bi-house-door me-1"></i> Welcome
                      </button>
                    </li>
                    <li className="nav-item">
                      <button 
                        className={`nav-link ${activeTab === 'login' ? 'active' : ''}`}
                        onClick={() => setActiveTab('login')}
                      >
                        <i className="bi bi-box-arrow-in-right me-1"></i> Login
                      </button>
                    </li>
                    <li className="nav-item">
                      <button 
                        className={`nav-link ${activeTab === 'register' ? 'active' : ''}`}
                        onClick={() => setActiveTab('register')}
                      >
                        <i className="bi bi-person-plus me-1"></i> Sign Up
                      </button>
                    </li>
                  </ul>
                </div>
              )}
            </div>
            
            <div className="col-lg-6">
              <div className="auth-container">
                {!isAuthenticated && activeTab === 'login' && (
                  <div className="auth-card">
                    <LoginForm onSuccess={handleAuthSuccess} />
                  </div>
                )}
                
                {!isAuthenticated && activeTab === 'register' && (
                  <div className="auth-card">
                    <RegisterForm onSuccess={() => setActiveTab('login')} />
                  </div>
                )}
                
                {(isAuthenticated || activeTab === 'welcome') && (
                  <div className="welcome-image">
                    <img src="https://placehold.co/600x400/e9ecef/6c757d?text=Social+App+Illustration" alt="Welcome Image" className="img-fluid rounded shadow" />
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="features-section py-5 bg-light">
        <div className="container">
          <h2 className="text-center mb-5">Why Choose SocialApp?</h2>
          
          <div className="row g-4">
            <div className="col-md-4">
              <div className="feature-card">
                <div className="feature-icon">
                  <i className="bi bi-people-fill"></i>
                </div>
                <h3 className="feature-title">Connect</h3>
                <p className="feature-text">
                  Connect with friends, family, and interesting people around the world.
                </p>
              </div>
            </div>
            
            <div className="col-md-4">
              <div className="feature-card">
                <div className="feature-icon">
                  <i className="bi bi-share-fill"></i>
                </div>
                <h3 className="feature-title">Share</h3>
                <p className="feature-text">
                  Share your thoughts, experiences, and meaningful moments with your network.
                </p>
              </div>
            </div>
            
            <div className="col-md-4">
              <div className="feature-card">
                <div className="feature-icon">
                  <i className="bi bi-compass-fill"></i>
                </div>
                <h3 className="feature-title">Discover</h3>
                <p className="feature-text">
                  Discover new content, ideas, and people based on your interests.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Home;
