
/**
 * Validates an email address format
 * @param {string} email - The email to validate
 * @returns {boolean} - True if valid, false otherwise
 */
export const isValidEmailFormat = (email) => {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email);
};

/**
 * Validates a password against common requirements
 * @param {string} password - The password to validate
 * @returns {object} - Object containing validation results
 */
export const validatePassword = (password) => {
  const results = {
    isValid: true,
    hasMinLength: password.length >= 6,
    hasUpperCase: /[A-Z]/.test(password),
    hasLowerCase: /[a-z]/.test(password),
    hasNumber: /\d/.test(password),
    hasSpecialChar: /[!@#$%^&*(),.?":{}|<>]/.test(password),
  };
  
  results.isValid = results.hasMinLength;
  
  // Recommended but not required
  results.strength = 
    (results.hasUpperCase ? 1 : 0) +
    (results.hasLowerCase ? 1 : 0) +
    (results.hasNumber ? 1 : 0) +
    (results.hasSpecialChar ? 1 : 0);
  
  return results;
};

/**
 * Calculates password strength on a scale of 0-4
 * @param {string} password - The password to evaluate
 * @returns {number} - Strength score from 0 (weakest) to 4 (strongest)
 */
export const getPasswordStrength = (password) => {
  if (!password) return 0;
  
  const { strength } = validatePassword(password);
  return strength;
};

/**
 * Get appropriate color class for password strength
 * @param {number} strength - Password strength (0-4)
 * @returns {string} - CSS class name for the strength indicator
 */
export const getPasswordStrengthClass = (strength) => {
  switch (strength) {
    case 0:
      return 'bg-danger';
    case 1:
      return 'bg-warning';
    case 2:
      return 'bg-info';
    case 3:
      return 'bg-primary';
    case 4:
      return 'bg-success';
    default:
      return 'bg-secondary';
  }
};

/**
 * Get text description for password strength
 * @param {number} strength - Password strength (0-4)
 * @returns {string} - Text description of password strength
 */
export const getPasswordStrengthText = (strength) => {
  switch (strength) {
    case 0:
      return 'Very Weak';
    case 1:
      return 'Weak';
    case 2:
      return 'Fair';
    case 3:
      return 'Good';
    case 4:
      return 'Strong';
    default:
      return '';
  }
};
