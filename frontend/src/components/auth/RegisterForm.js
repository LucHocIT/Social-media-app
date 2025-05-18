import React, { useState } from 'react';
import { useAuth } from '../../services/AuthContext';

const RegisterForm = ({ onSuccess }) => {
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
  const { register } = useAuth();

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
      
      // Call onSuccess after a short delay
      setTimeout(() => {
        if (onSuccess) onSuccess();
      }, 2000);
    } catch (err) {
      console.error('Registration error:', err);
      setError(err.response?.data?.message || 'Failed to register. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-form-container">
      <h2 className="text-center mb-4">Create Account</h2>
      
      {error && (
        <div className="alert alert-danger">{error}</div>
      )}
      
      {success && (
        <div className="alert alert-success">{success}</div>
      )}
      
      <form onSubmit={handleSubmit}>
        <div className="row">
          <div className="col-md-6 mb-3">
            <label htmlFor="firstName" className="form-label">First Name</label>
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
          
          <div className="col-md-6 mb-3">
            <label htmlFor="lastName" className="form-label">Last Name</label>
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
        
        <div className="mb-3">
          <label htmlFor="username" className="form-label">Username</label>
          <div className="input-group">
            <span className="input-group-text"><i className="bi bi-person"></i></span>
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
            <span className="input-group-text"><i className="bi bi-envelope"></i></span>
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
            <span className="input-group-text"><i className="bi bi-lock"></i></span>
            <input
              type="password"
              className="form-control"
              id="password"
              name="password"
              placeholder="Create a password"
              value={formData.password}
              onChange={handleChange}
              required
            />
          </div>
        </div>
        
        <div className="mb-3">
          <label htmlFor="confirmPassword" className="form-label">Confirm Password</label>
          <div className="input-group">
            <span className="input-group-text"><i className="bi bi-shield-lock"></i></span>
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
          className="btn btn-primary w-100" 
          disabled={loading}
        >
          {loading ? <><i className="bi bi-hourglass-split me-1"></i> Registering...</> : <><i className="bi bi-person-plus me-1"></i> Register</>}
        </button>
      </form>
    </div>
  );
};

export default RegisterForm;
