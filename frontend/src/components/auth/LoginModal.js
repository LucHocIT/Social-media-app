import React, { useState, useEffect } from 'react';
import { useAuth } from '../../services/AuthContext';
import '../../../src/assets/css/auth-animations.css';

const LoginModal = ({ isOpen, onClose, onSwitchToRegister }) => {
  const [formData, setFormData] = useState({
    username: '',
    password: ''
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [modalClass, setModalClass] = useState('auth-modal');
  const [overlayClass, setOverlayClass] = useState('auth-modal-overlay');
  const [isClosing, setIsClosing] = useState(false);
  const { login } = useAuth();

  useEffect(() => {
    if (isOpen) {
      setIsClosing(false);
      setModalClass('auth-modal');
      setOverlayClass('auth-modal-overlay');
      document.body.style.overflow = 'hidden';
    } else if (!isClosing) {
      // Do nothing if not open and not closing
      return;
    } else {
      // Wait for animation to complete before removing
      const timer = setTimeout(() => {
        document.body.style.overflow = 'auto';
      }, 300);
      return () => clearTimeout(timer);
    }
  }, [isOpen, isClosing]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    
    try {
      await login(formData.username, formData.password);
      handleClose();
    } catch (err) {
      console.error('Login error:', err);
      setError(err.response?.data?.message || 'Failed to login. Please check your credentials.');
    } finally {
      setLoading(false);
    }
  };
  const handleClose = () => {
    setIsClosing(true);
    setModalClass('auth-modal closing');
    setOverlayClass('auth-modal-overlay closing');
    setTimeout(() => {
      onClose();
      setFormData({ username: '', password: '' });
      setError('');
      setIsClosing(false);
    }, 300);
  };
  const handleSwitchToRegister = (e) => {
    e.preventDefault();
    setIsClosing(true);
    setModalClass('auth-modal closing');
    setOverlayClass('auth-modal-overlay closing');
    
    setTimeout(() => {
      onSwitchToRegister();
      setIsClosing(false);
      setFormData({ username: '', password: '' });
      setError('');
    }, 300);
  };
  return (
    <>
      {(isOpen || isClosing) && (
        <div className={overlayClass} onClick={handleClose}>
          <div className={modalClass} onClick={e => e.stopPropagation()}>
            <div className="auth-modal-header">
              <h2 className="auth-modal-title">
                <i className="bi bi-box-arrow-in-right"></i> Welcome Back
              </h2>
              <button className="btn-close" onClick={handleClose}></button>
            </div>
            
            <div className="auth-modal-body">
              {error && (
                <div className="alert alert-danger animate__animated animate__fadeIn">
                  <i className="bi bi-exclamation-triangle-fill me-2"></i>
                  {error}
                </div>
              )}
              
              <form onSubmit={handleSubmit}>
                <div className="auth-form-group">
                  <label htmlFor="username" className="auth-form-label">Username</label>
                  <div className="auth-input-group">
                    <i className="bi bi-person auth-input-icon"></i>
                    <input
                      type="text"
                      className="auth-form-input"
                      id="username"
                      name="username"
                      placeholder="Enter your username"
                      value={formData.username}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <div className="auth-form-group">
                  <div className="d-flex justify-content-between align-items-center">
                    <label htmlFor="password" className="auth-form-label">Password</label>
                    <a href="#" className="auth-switch-link">Forgot password?</a>
                  </div>
                  <div className="auth-input-group">
                    <i className="bi bi-lock auth-input-icon"></i>
                    <input
                      type="password"
                      className="auth-form-input"
                      id="password"
                      name="password"
                      placeholder="Enter your password"
                      value={formData.password}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <div className="mb-4 form-check">
                  <input type="checkbox" className="form-check-input" id="rememberMe" />
                  <label className="form-check-label" htmlFor="rememberMe">Remember me</label>
                </div>
                
                <button 
                  type="submit" 
                  className="auth-submit-btn mb-3" 
                  disabled={loading}
                >
                  {loading ? (
                    <>
                      <span className="spinner-border spinner-border-sm spinner me-2" role="status" aria-hidden="true"></span>
                      Signing in...
                    </>
                  ) : (
                    <>Sign In</>
                  )}
                </button>
              </form>
              
              <div className="auth-separator">
                <span>or continue with</span>
              </div>
              
              <div className="social-auth-buttons">
                <button className="social-auth-btn google">
                  <i className="bi bi-google"></i>Google
                </button>
                <button className="social-auth-btn facebook">
                  <i className="bi bi-facebook"></i>Facebook
                </button>
                <button className="social-auth-btn twitter">
                  <i className="bi bi-twitter"></i>Twitter
                </button>
              </div>
            </div>
            
            <div className="auth-modal-footer">
              <p className="mb-0">
                Don't have an account?{' '}
                <a href="#" className="auth-switch-link" onClick={handleSwitchToRegister}>
                  Create an account
                </a>
              </p>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default LoginModal;
