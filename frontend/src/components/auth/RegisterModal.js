import React, { useState, useEffect } from 'react';
import { useAuth } from '../../services/AuthContext';
import { authApi } from '../../services/api';
import { getPasswordStrength, getPasswordStrengthClass, getPasswordStrengthText } from '../../utils/validation';
import { DEV_CONFIG } from '../../utils/devConfig';
import '../../../src/assets/css/auth-animations.css';

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
  const [overlayClass, setOverlayClass] = useState('auth-modal-overlay');
  const [isClosing, setIsClosing] = useState(false);
  const [isVerifyingEmail, setIsVerifyingEmail] = useState(false);
  const [emailValid, setEmailValid] = useState(null);
  const [passwordStrength, setPasswordStrength] = useState(0);
  const { register } = useAuth();

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
  }, [isOpen, isClosing]);  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
    
    // Reset email validation when email changes
    if (name === 'email') {
      setEmailValid(null);
      
      // Only trigger verification if email has some content and appears valid
      const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
      if (value && emailRegex.test(value)) {
        // In development mode, skip verification and consider the email valid
        if (DEV_CONFIG.SKIP_EMAIL_VERIFICATION) {
          console.log("DEV MODE: Skipping email verification");
          setEmailValid(true);
          return;
        }

        // Debounce the email verification
        let debounceTimer;
        clearTimeout(debounceTimer);
        
        // Store the current value for comparison in the async handler
        const currentEmail = value;
        
        debounceTimer = setTimeout(async () => {
          // Only proceed if the email hasn't changed during the debounce period
          if (currentEmail === formData.email) {              try {
              setIsVerifyingEmail(true);
              console.log("Verifying email:", currentEmail);
              const response = await authApi.verifyEmail(currentEmail);
              console.log("Verification response:", response);
              // Only update if this is still the current email
              if (currentEmail === formData.email) {
                setEmailValid(true);
              }
            } catch (err) {
              console.error('Email verification error:', err);
              // Only update if this is still the current email
              if (currentEmail === formData.email) {
                // If it's a network error or any other error, we'll now consider the email valid
                // to allow the user to proceed with registration
                if (err.code === 'ERR_NETWORK' || err.code === 'ERR_CONNECTION_REFUSED' || !err.response) {
                  console.log("Network error detected, allowing registration");
                  setEmailValid(true);
                } else if (err.response?.status === 400) {
                  // If the server specifically says the email is invalid or already used
                  setEmailValid(false);
                } else {
                  // For any other error, allow registration
                  console.log("Other error detected, allowing registration");
                  setEmailValid(true);
                }
              }
            } finally {
              if (currentEmail === formData.email) {
                setIsVerifyingEmail(false);
              }
            }
          }
        }, 800);
      }
    }
    
    // Update password strength when password changes
    if (name === 'password') {
      setPasswordStrength(getPasswordStrength(value));
    }
  };  const validateForm = () => {
    let errors = [];
    
    if (formData.password !== formData.confirmPassword) {
      errors.push('Passwords do not match');
    }
    
    if (formData.password.length < 6) {
      errors.push('Password must be at least 6 characters long');
    }
    
    // Validate email format using regex instead of relying only on emailValid
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(formData.email)) {
      errors.push('Please enter a valid email address format');
    } else if (!DEV_CONFIG.SKIP_EMAIL_VERIFICATION && emailValid === false) {
      // Only check emailValid if it's explicitly false and we're not in dev mode
      errors.push('Please use a valid email address that is not already registered');
    }
    
    if (errors.length > 0) {
      setError(errors.join('. '));
      return false;
    }
    
    return true;
  };  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    
    if (!validateForm()) {
      return;
    }
    
    setLoading(true);
    
    try {
      // Log the data before sending for debugging
      console.log("Sending registration data:", formData);
      
      // Removing confirmPassword as it's not needed in the API call
      const { confirmPassword, ...registrationData } = formData;
      
      // Set a longer timeout for the registration request (30 seconds)
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 30000);
      
      try {
        const response = await register(registrationData);
        console.log("Registration success:", response);
        clearTimeout(timeoutId);
        
        setSuccess('Registration successful! You can now login.');
        
        // Switch to login after a short delay
        setTimeout(() => {
          handleClose();
          setTimeout(() => {
            onSwitchToLogin();
          }, 300);
        }, 2000);
      } catch (registerError) {
        clearTimeout(timeoutId);
        throw registerError;
      }
    } catch (err) {
      console.error('Registration error:', err);
      
      // Handle specific error cases
      if (err.name === 'AbortError' || err.code === 'ECONNABORTED') {
        setError('Registration request timed out. The server might be busy. Please try again later.');
      } else if (err.code === 'ERR_NETWORK' || err.code === 'ERR_CONNECTION_REFUSED' || !err.response) {
        setError('Network connection error. Please check your connection and try again later.');
      } else if (err.response) {
        if (err.response.status === 504) {
          setError('The server is taking too long to respond. Please try again later.');
        } else if (err.response.status === 500) {
          setError('Server error occurred. Our team has been notified. Please try again later.');
        } else {
          console.log("Error response data:", err.response.data);
          console.log("Error status:", err.response.status);
          setError(err.response.data?.message || 'Failed to register. Please try again.');
        }
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
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
      setIsClosing(false);
      setEmailValid(null);
    }, 300);
  };
  const handleSwitchToLogin = (e) => {
    e.preventDefault();
    setIsClosing(true);
    setModalClass('auth-modal closing');
    setOverlayClass('auth-modal-overlay closing');
    
    setTimeout(() => {
      onSwitchToLogin();
      setIsClosing(false);
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
      setEmailValid(null);
    }, 300);
  };
  return (
    <>
      {(isOpen || isClosing) && (
        <div className={overlayClass} onClick={handleClose}>
          <div className={modalClass} onClick={e => e.stopPropagation()}>
            <div className="auth-modal-header">
              <h2 className="auth-modal-title">
                <i className="bi bi-person-plus"></i> Create Account
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
                  <div className="col-md-6">
                    <div className="auth-form-group">
                      <label htmlFor="firstName" className="auth-form-label">First Name</label>
                      <div className="auth-input-group">
                        <i className="bi bi-person auth-input-icon"></i>
                        <input
                          type="text"
                          className="auth-form-input"
                          id="firstName"
                          name="firstName"
                          placeholder="First name"
                          value={formData.firstName}
                          onChange={handleChange}
                          required
                        />
                      </div>
                    </div>
                  </div>
                  
                  <div className="col-md-6">
                    <div className="auth-form-group">
                      <label htmlFor="lastName" className="auth-form-label">Last Name</label>
                      <div className="auth-input-group">
                        <i className="bi bi-person auth-input-icon"></i>
                        <input
                          type="text"
                          className="auth-form-input"
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
                </div>
                
                <div className="auth-form-group">
                  <label htmlFor="username" className="auth-form-label">Username</label>
                  <div className="auth-input-group">
                    <i className="bi bi-person-badge auth-input-icon"></i>
                    <input
                      type="text"
                      className="auth-form-input"
                      id="username"
                      name="username"
                      placeholder="Choose a username"
                      value={formData.username}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
                
                <div className="auth-form-group">
                  <label htmlFor="email" className="auth-form-label">Email</label>
                  <div className="auth-input-group">
                    <i className="bi bi-envelope auth-input-icon"></i>
                    <input
                      type="email"
                      className={`auth-form-input ${emailValid === true ? 'is-valid' : ''} ${emailValid === false ? 'is-invalid' : ''}`}
                      id="email"
                      name="email"
                      placeholder="Your email address"
                      value={formData.email}
                      onChange={handleChange}
                      required
                    />
                    
                    {/* Email verification indicator */}
                    {formData.email && (
                      <div className="email-verification-indicator">
                        {isVerifyingEmail && <i className="bi bi-arrow-repeat verification-loading"></i>}
                        {!isVerifyingEmail && emailValid === true && <i className="bi bi-check-circle-fill verification-valid"></i>}
                        {!isVerifyingEmail && emailValid === false && <i className="bi bi-x-circle-fill verification-invalid"></i>}
                      </div>
                    )}
                    
                    {/* Email verification tooltip */}
                    <div className="verification-tooltip">
                      {isVerifyingEmail && "Verifying email..."}
                      {!isVerifyingEmail && emailValid === true && "Email is valid and not already in use."}
                      {!isVerifyingEmail && emailValid === false && "Email is invalid or already in use."}
                    </div>
                  </div>
                  
                  {!isVerifyingEmail && emailValid === false && (
                    <div className="auth-form-error">
                      Please enter a valid email that is not already registered.
                    </div>
                  )}
                </div>
                  <div className="auth-form-group">
                  <label htmlFor="password" className="auth-form-label">Password</label>
                  <div className="auth-input-group">
                    <i className="bi bi-lock auth-input-icon"></i>
                    <input
                      type="password"
                      className="auth-form-input"
                      id="password"
                      name="password"
                      placeholder="Create a strong password"
                      value={formData.password}
                      onChange={handleChange}
                      required
                    />
                  </div>
                  {formData.password && formData.password.length < 6 && (
                    <div className="auth-form-error">
                      Password must be at least 6 characters long.
                    </div>
                  )}
                  
                  {formData.password && (
                    <div className="mt-2">
                      <div className="password-strength-meter">
                        <div 
                          className={`password-strength-fill ${getPasswordStrengthClass(passwordStrength)}`}
                          style={{ width: `${(passwordStrength / 4) * 100}%` }}
                        ></div>
                      </div>
                      <small className="text-muted">
                        Password strength: <span className="fw-medium">{getPasswordStrengthText(passwordStrength)}</span>
                      </small>
                    </div>
                  )}
                </div>
                
                <div className="auth-form-group">
                  <label htmlFor="confirmPassword" className="auth-form-label">Confirm Password</label>
                  <div className="auth-input-group">
                    <i className="bi bi-shield-lock auth-input-icon"></i>
                    <input
                      type="password"
                      className={`auth-form-input ${
                        formData.confirmPassword && formData.password === formData.confirmPassword 
                        ? 'is-valid' 
                        : formData.confirmPassword ? 'is-invalid' : ''
                      }`}
                      id="confirmPassword"
                      name="confirmPassword"
                      placeholder="Confirm your password"
                      value={formData.confirmPassword}
                      onChange={handleChange}
                      required
                    />
                    
                    {formData.confirmPassword && formData.password === formData.confirmPassword && (
                      <div className="auth-input-feedback valid">
                        <i className="bi bi-check-circle-fill"></i>
                      </div>
                    )}
                    
                    {formData.confirmPassword && formData.password !== formData.confirmPassword && (
                      <div className="auth-input-feedback invalid">
                        <i className="bi bi-x-circle-fill"></i>
                      </div>
                    )}
                  </div>
                  
                  {formData.confirmPassword && formData.password !== formData.confirmPassword && (
                    <div className="auth-form-error">
                      Passwords do not match.
                    </div>
                  )}
                </div>
                
                <div className="auth-form-group">
                  <label className="auth-form-label">Password Strength</label>
                  <div className="password-strength-meter">
                    <div className={`password-strength-bar ${getPasswordStrengthClass(passwordStrength)}`} style={{ width: `${passwordStrength}%` }}></div>
                  </div>
                  <div className="password-strength-text">
                    {formData.password && `Strength: ${getPasswordStrengthText(passwordStrength)}`}
                  </div>
                </div>
                  <button 
                  type="submit" 
                  className="auth-submit-btn mb-3" 
                  disabled={loading || 
                    (!DEV_CONFIG.SKIP_EMAIL_VERIFICATION && emailValid === false) || 
                    (!DEV_CONFIG.SKIP_EMAIL_VERIFICATION && formData.email && emailValid === null && isVerifyingEmail)
                  }
                >
                  {loading ? (
                    <>
                      <span className="spinner-border spinner-border-sm spinner me-2" role="status" aria-hidden="true"></span>
                      Creating Account...
                    </>
                  ) : (
                    <>Create Account</>
                  )}
                </button>
              </form>
            </div>
            
            <div className="auth-modal-footer">
              <p className="mb-0">
                Already have an account?{' '}
                <a href="#" className="auth-switch-link" onClick={handleSwitchToLogin}>
                  Sign in
                </a>
              </p>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default RegisterModal;
