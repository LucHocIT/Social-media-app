import React from 'react';
import { useAuth } from '../../services/AuthContext';

const Profile = () => {
  const { currentUser } = useAuth();

  if (!currentUser) {
    return <div className="text-center">Loading profile...</div>;
  }

  return (
    <div className="row">
      <div className="col-md-4">
        <div className="card">
          <div className="card-body text-center">
            <div className="mb-3">
              <img 
                src={currentUser.profilePicture || "https://via.placeholder.com/150"} 
                alt="Profile" 
                className="avatar img-fluid mb-3"
              />
            </div>
            
            <h3>{currentUser.firstName} {currentUser.lastName}</h3>
            <p className="text-muted">@{currentUser.username}</p>
            
            <div className="d-grid">
              <button className="btn btn-outline-primary">Edit Profile</button>
            </div>
          </div>
        </div>
        
        <div className="card mt-3">
          <div className="card-body">
            <h5 className="card-title">About</h5>
            <p>{currentUser.bio || 'No bio provided yet.'}</p>
            
            <h6>Email</h6>
            <p className="text-muted">{currentUser.email}</p>
            
            <h6>Joined</h6>
            <p className="text-muted">
              {new Date(currentUser.createdAt).toLocaleDateString()}
            </p>
          </div>
        </div>
      </div>
      
      <div className="col-md-8">
        <div className="card">
          <div className="card-body">
            <h4 className="card-title">My Posts</h4>
            
            <div className="mb-4">
              <textarea
                className="form-control"
                rows="3"
                placeholder="What's on your mind?"
              ></textarea>
              <div className="d-flex justify-content-end mt-2">
                <button className="btn btn-primary">Post</button>
              </div>
            </div>
            
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
                  You're not following anyone yet.
                </p>
              </div>
            </div>
          </div>
          
          <div className="col-md-6">
            <div className="card">
              <div className="card-body">
                <h5 className="card-title">Followers</h5>
                <p className="card-text">
                  You don't have any followers yet.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Profile;
