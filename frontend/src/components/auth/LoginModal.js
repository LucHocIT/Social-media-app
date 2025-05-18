import React, { useState, useEffect } from 'react';
import { useAuth } from '../../services/AuthContext';

const LoginModal = ({ isOpen, onClose, onSwitchToRegister }) => {
  const [formData, setFormData] = useState({
    username: '',
    password: ''
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [modalClass, setModalClass] = useState('auth-modal');
  const { login } = useAuth();

  useEffect(() => {
    if (isOpen) {
      setModalClass('auth-modal show');
      document.body.style.overflow = 'hidden';
    } else {
      setModalClass('auth-modal');
      // Wait for animation to complete before removing
      const timer = setTimeout(() => {
        document.body.style.overflow = 'auto';
      }, 300);
      return () => clearTimeout(timer);
    }
  }, [isOpen]);

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
    setModalClass('auth-modal');
    setTimeout(() => {
      onClose();
      setFormData({ username: '', password: '' });
      setError('');
    }, 300);
  };

  const handleSwitchToRegister = (e) => {
    e.preventDefault();
    handleClose();
    setTimeout(() => {
      onSwitchToRegister();
    }, 300);
  };

  return (
    <>
      {isOpen && (
        <div className="auth-modal-overlay" onClick={handleClose}>
          <div className={modalClass} onClick={e => e.stopPropagation()}>
            <div className="auth-modal-header">
              <h2 className="auth-modal-title">
                <i className="bi bi-box-arrow-in-right text-primary me-2"></i> Login
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
                <div className="mb-4">
                  <label htmlFor="username" className="form-label">Username</label>
                  <div className="input-group input-group-lg">
                    <span className="input-group-text bg-light">
                      <i className="bi bi-person text-primary"></i>
                    </span>
                    <input
                      type="text"
                      className="form-control"
                      id="username"
                      name="username"
                      placeholder="Enter your username"
                      value={formData.username}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <div className="mb-4">
                  <div className="d-flex justify-content-between align-items-center">
                    <label htmlFor="password" className="form-label">Password</label>
                    <a href="#" className="text-sm text-primary text-decoration-none">Forgot password?</a>
                  </div>
                  <div className="input-group input-group-lg">
                    <span className="input-group-text bg-light">
                      <i className="bi bi-lock text-primary"></i>
                    </span>
                    <input
                      type="password"
                      className="form-control"
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
                  className="btn btn-primary btn-lg w-100 mb-3" 
                  disabled={loading}
                >
                  {loading ? (
                    <>
                      <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                      Logging in...
                    </>
                  ) : (
                    <>Login</>
                  )}
                </button>
              </form>
              
              <div className="text-center mt-4">
                <p className="text-muted">
                  Don't have an account?{' '}
                  <a href="#" className="text-primary fw-bold" onClick={handleSwitchToRegister}>
                    Sign up
                  </a>
                </p>
              </div>

              <div className="auth-separator">
                <span>or continue with</span>
              </div>
              
              <div className="social-auth-buttons">
                <button className="btn btn-outline-secondary me-2">
                  <i className="bi bi-google me-2"></i>Google
                </button>
                <button className="btn btn-outline-secondary me-2">
                  <i className="bi bi-facebook me-2"></i>Facebook
                </button>
                <button className="btn btn-outline-secondary">
                  <i className="bi bi-twitter me-2"></i>Twitter
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default LoginModal;
