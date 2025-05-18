import React, { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { userApi } from '../../services/api';

const UserDetails = () => {
  const { id } = useParams();
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchUser = async () => {
      try {
        const response = await userApi.getUserById(id);
        setUser(response.data);
      } catch (err) {
        console.error('Error fetching user:', err);
        setError('Failed to load user information. Please try again later.');
      } finally {
        setLoading(false);
      }
    };

    fetchUser();
  }, [id]);

  if (loading) {
    return (
      <div className="text-center">
        <div className="spinner-border" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="alert alert-danger" role="alert">
        {error}
      </div>
    );
  }

  if (!user) {
    return (
      <div className="alert alert-warning" role="alert">
        User not found.
      </div>
    );
  }

  return (
    <div className="row">
      <div className="col-md-4">
        <div className="card">
          <div className="card-body text-center">
            <div className="mb-3">
              <img 
                src={user.profilePicture || "https://via.placeholder.com/150"} 
                alt="Profile" 
                className="avatar img-fluid mb-3"
              />
            </div>
            
            <h3>{user.firstName} {user.lastName}</h3>
            <p className="text-muted">@{user.username}</p>
            
            <div className="d-grid gap-2">
              <button className="btn btn-primary">Follow</button>
              <button className="btn btn-outline-primary">Message</button>
            </div>
          </div>
        </div>
        
        <div className="card mt-3">
          <div className="card-body">
            <h5 className="card-title">About</h5>
            <p>{user.bio || 'No bio provided.'}</p>
            
            <h6>Joined</h6>
            <p className="text-muted">
              {new Date(user.createdAt).toLocaleDateString()}
            </p>
          </div>
        </div>
      </div>
      
      <div className="col-md-8">
        <div className="card">
          <div className="card-body">
            <h4 className="card-title">Recent Posts</h4>
            
            {/* Placeholder for user posts */}
            <div className="alert alert-info">
              Posts feature will be implemented soon!
            </div>
          </div>
        </div>
        
        <div className="row mt-4">
          <div className="col-md-6">
            <div className="card">
              <div className="card-body">
                <h5 className="card-title">Following</h5>
                <p className="card-text">
                  {user.username} isn't following anyone yet.
                </p>
              </div>
            </div>
          </div>
          
          <div className="col-md-6">
            <div className="card">
              <div className="card-body">
                <h5 className="card-title">Followers</h5>
                <p className="card-text">
                  {user.username} doesn't have any followers yet.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default UserDetails;
