import axios from 'axios';

// Create an axios instance with the base URL
// When using webpack dev server proxy, we use relative paths
// This ensures requests go through the proxy defined in webpack.config.js
const api = axios.create({
  baseURL: '/api',
  // Increased timeout for slower networks and processing-intensive operations
  timeout: 30000, // 30 seconds
});

// Add retry functionality for failed requests
api.interceptors.response.use(undefined, async (error) => {
  const { config, response } = error;
  
  // Only retry GET requests or ignore if we've already retried
  if (!config || !config.method || config.method.toLowerCase() !== 'get' || config.__isRetryRequest) {
    return Promise.reject(error);
  }

  // Define retry count and limits
  config.__isRetryRequest = true;
  config.__retryCount = config.__retryCount || 0;
  const maxRetries = 2;
  
  // If we haven't exceeded max retries and it's a network error or 5xx error
  if (config.__retryCount < maxRetries && 
     (error.code === 'ECONNABORTED' || error.code === 'ERR_NETWORK' || 
      (response && response.status >= 500))) {
    config.__retryCount += 1;
    
    // Wait before retrying (progressive delay)
    const delay = config.__retryCount * 1000;
    console.log(`Retrying request (${config.__retryCount}/${maxRetries}) after ${delay}ms`);
    
    return new Promise(resolve => setTimeout(() => resolve(axios(config)), delay));
  }
  
  return Promise.reject(error);
});

// Thêm interceptor cho yêu cầu để tự động thêm token vào header
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

export const authApi = {
  register: async (userData) => {
    console.log('Registering user with data:', userData);
    try {
      // For important operations like registration, implement manual retry logic
      const maxRetries = 2;
      let retryCount = 0;
      let lastError = null;
      
      while (retryCount <= maxRetries) {
        try {
          const response = await api.post('/auth/register', userData, {
            timeout: 45000, // Extended timeout for registration (45 seconds)
          });
          console.log('Register API response:', response);
          return response;
        } catch (error) {
          lastError = error;
          
          // Only retry on network errors or server errors (5xx)
          if ((error.code === 'ECONNABORTED' || error.code === 'ERR_NETWORK' || 
              (error.response && error.response.status >= 500)) && 
              retryCount < maxRetries) {
            
            retryCount++;
            const delay = retryCount * 2000; // Progressive delay: 2s, 4s
            console.log(`Registration failed. Retrying (${retryCount}/${maxRetries}) after ${delay}ms`);
            await new Promise(resolve => setTimeout(resolve, delay));
            continue;
          }
          
          // If error is not retryable or we're out of retries, throw it
          throw error;
        }
      }
      
      // If we've exhausted all retries and still have an error
      throw lastError;
    } catch (error) {
      console.error('Register API error after retries:', error);
      throw error;
    }
  },
  login: (credentials) => api.post('/auth/login', credentials),
  logout: () => api.post('/auth/logout'),
  getCurrentUser: () => api.get('/users/me'),
  verifyEmail: async (email) => {
    console.log('Verifying email:', email);
    try {
      // For email verification, also implement retry logic
      const maxRetries = 2;
      let retryCount = 0;
      let lastError = null;
      
      while (retryCount <= maxRetries) {
        try {
          const response = await api.post('/auth/verifyemail', { email }, {
            timeout: 20000, // Extended timeout for email verification (20 seconds)
          });
          console.log('Email verification response:', response);
          return response;
        } catch (error) {
          lastError = error;
          
          // Only retry on network errors or server errors
          if ((error.code === 'ECONNABORTED' || error.code === 'ERR_NETWORK' || 
              (error.response && error.response.status >= 500)) && 
              retryCount < maxRetries) {
            
            retryCount++;
            const delay = retryCount * 1500; // Progressive delay: 1.5s, 3s
            console.log(`Email verification failed. Retrying (${retryCount}/${maxRetries}) after ${delay}ms`);
            await new Promise(resolve => setTimeout(resolve, delay));
            continue;
          }
          
          // If error is not retryable or we're out of retries, throw it
          throw error;
        }
      }
        // If we've exhausted all retries and still have an error
      throw lastError;
    } catch (error) {
      console.error('Email verification error after retries:', error);
      throw error;
    }
  }
};

export const userApi = {
  getUserById: (id) => api.get(`/users/${id}`)
};

export default api;
