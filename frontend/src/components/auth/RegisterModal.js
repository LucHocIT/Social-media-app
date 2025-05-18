import React, { useState, useEffect } from 'react';
import { useAuth } from '../../services/AuthContext';

const RegisterModal = ({ isOpen, onClose, onSwitchToLogin }) => {
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    confirmPassword: '',
    firstName: '',
    lastName: ''
  });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);
  const [modalClass, setModalClass] = useState('auth-modal');
  const { register } = useAuth();

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

  const validateForm = () => {
    if (formData.password !== formData.confirmPassword) {
      setError('Passwords do not match');
      return false;
    }
    
    if (formData.password.length < 6) {
      setError('Password must be at least 6 characters long');
      return false;
    }
    
    return true;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    
    if (!validateForm()) {
      return;
    }
    
    setLoading(true);
    
    try {
      // Removing confirmPassword as it's not needed in the API call
      const { confirmPassword, ...registrationData } = formData;
      
      await register(registrationData);
      setSuccess('Registration successful! You can now login.');
      
      // Switch to login after a short delay
      setTimeout(() => {
        handleClose();
        setTimeout(() => {
          onSwitchToLogin();
        }, 300);
      }, 2000);
    } catch (err) {
      console.error('Registration error:', err);
      setError(err.response?.data?.message || 'Failed to register. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    setModalClass('auth-modal');
    setTimeout(() => {
      onClose();
      setFormData({
        username: '',
        email: '',
        password: '',
        confirmPassword: '',
        firstName: '',
        lastName: ''
      });
      setError('');
      setSuccess('');
    }, 300);
  };

  const handleSwitchToLogin = (e) => {
    e.preventDefault();
    handleClose();
    setTimeout(() => {
      onSwitchToLogin();
    }, 300);
  };

  return (
    <>
      {isOpen && (
        <div className="auth-modal-overlay" onClick={handleClose}>
          <div className={modalClass} onClick={e => e.stopPropagation()}>
            <div className="auth-modal-header">
              <h2 className="auth-modal-title">
                <i className="bi bi-person-plus text-primary me-2"></i> Create Account
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
              
              {success && (
                <div className="alert alert-success animate__animated animate__fadeIn">
                  <i className="bi bi-check-circle-fill me-2"></i>
                  {success}
                </div>
              )}
              
              <form onSubmit={handleSubmit}>
                <div className="row">
                  <div className="col-md-6 mb-3">
                    <label htmlFor="firstName" className="form-label">First Name</label>
                    <div className="input-group">
                      <span className="input-group-text bg-light">
                        <i className="bi bi-person text-primary"></i>
                      </span>
                      <input
                        type="text"
                        className="form-control"
                        id="firstName"
                        name="firstName"
                        placeholder="First name"
                        value={formData.firstName}
                        onChange={handleChange}
                        required
                      />
                    </div>
                  </div>
                  
                  <div className="col-md-6 mb-3">
                    <label htmlFor="lastName" className="form-label">Last Name</label>
                    <div className="input-group">
                      <span className="input-group-text bg-light">
                        <i className="bi bi-person text-primary"></i>
                      </span>
                      <input
                        type="text"
                        className="form-control"
                        id="lastName"
                        name="lastName"
                        placeholder="Last name"
                        value={formData.lastName}
                        onChange={handleChange}
                        required
                      />
                    </div>
                  </div>
                </div>
                
                <div className="mb-3">
                  <label htmlFor="username" className="form-label">Username</label>
                  <div className="input-group">
                    <span className="input-group-text bg-light">
                      <i className="bi bi-person-badge text-primary"></i>
                    </span>
                    <input
                      type="text"
                      className="form-control"
                      id="username"
                      name="username"
                      placeholder="Choose a username"
                      value={formData.username}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <div className="mb-3">
                  <label htmlFor="email" className="form-label">Email</label>
                  <div className="input-group">
                    <span className="input-group-text bg-light">
                      <i className="bi bi-envelope text-primary"></i>
                    </span>
                    <input
                      type="email"
                      className="form-control"
                      id="email"
                      name="email"
                      placeholder="Your email address"
                      value={formData.email}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <div className="mb-3">
                  <label htmlFor="password" className="form-label">Password</label>
                  <div className="input-group">
                    <span className="input-group-text bg-light">
                      <i className="bi bi-lock text-primary"></i>
                    </span>
                    <input
                      type="password"
                      className="form-control"
                      id="password"
                      name="password"
                      placeholder="Create a strong password"
                      value={formData.password}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <div className="mb-4">
                  <label htmlFor="confirmPassword" className="form-label">Confirm Password</label>
                  <div className="input-group">
                    <span className="input-group-text bg-light">
                      <i className="bi bi-shield-lock text-primary"></i>
                    </span>
                    <input
                      type="password"
                      className="form-control"
                      id="confirmPassword"
                      name="confirmPassword"
                      placeholder="Confirm your password"
                      value={formData.confirmPassword}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <button 
                  type="submit" 
                  className="btn btn-primary btn-lg w-100 mb-3" 
                  disabled={loading}
                >
                  {loading ? (
                    <>
                      <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                      Registering...
                    </>
                  ) : (
                    <>Create Account</>
                  )}
                </button>
              </form>
              
              <div className="text-center mt-4">
                <p className="text-muted">
                  Already have an account?{' '}
                  <a href="#" className="text-primary fw-bold" onClick={handleSwitchToLogin}>
                    Login
                  </a>
                </p>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default RegisterModal;
