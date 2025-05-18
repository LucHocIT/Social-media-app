import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../services/AuthContext';

const Navbar = () => {
  const { isAuthenticated, currentUser, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/');
  };

  return (
    <nav className="navbar navbar-expand-lg navbar-dark bg-primary sticky-top">
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
                    <span className="text-white fw-medium">
                      {currentUser?.firstName || currentUser?.username}
                    </span>
                  </div>
                </li>
              </>
            ) : (
              <>
                <li className="nav-item">
                  <Link className="nav-link" to="/" onClick={() => document.getElementById('login-section')?.scrollIntoView({ behavior: 'smooth' })}>
                    <i className="bi bi-box-arrow-in-right me-1"></i> Login
                  </Link>
                </li>
                <li className="nav-item">
                  <Link className="nav-link btn btn-outline-light ms-2 px-3 py-1" to="/" onClick={() => document.getElementById('register-section')?.scrollIntoView({ behavior: 'smooth' })}>
                    <i className="bi bi-person-plus me-1"></i> Sign Up
                  </Link>
                </li>
              </>
            )}
          </ul>
        </div>
      </div>
    </nav>
  );
};

export default Navbar;
