"""
Radar Data Service Module
Provides functionality to fetch NEXRAD radar data from AWS
"""
import datetime
import os
import boto3
import numpy as np
import pyart


class RadarDataService:
    """
    Service for fetching NEXRAD radar data
    """
    def __init__(self, data_folder="radar_data"):
        """
        Initialize the radar data service
        
        Parameters
        ----------
        data_folder : str
            Folder path to store downloaded radar data
        """
        self.data_folder = data_folder
        # Ensure the data folder exists
        if not os.path.exists(data_folder):
            os.makedirs(data_folder)
            
    def fetch_nexrad_data(self, radar_id, num_files=1):
        """
        Fetch NEXRAD radar data from AWS S3
        
        Parameters
        ----------
        radar_id : str
            NEXRAD radar station identifier (e.g., 'KDOX')
        num_files : int, optional
            Number of most recent files to download
            
        Returns
        -------
        list
            List of local file paths to downloaded radar data, or None if fetch fails
        """
        s3 = boto3.client("s3")
        bucket = "noaa-nexrad-level2"
        
        # Get current UTC date for constructing the prefix
        today = datetime.datetime.utcnow()
        file_prefix = f"{today.year}/{today.strftime('%m')}/{today.strftime('%d')}/{radar_id}/"
        
        try:
            # List objects in the S3 bucket with the specified prefix
            response = s3.list_objects_v2(Bucket=bucket, Prefix=file_prefix)
            
            if "Contents" in response:
                # Sort files by last modified time (most recent first)
                sorted_files = sorted(
                    response["Contents"],
                    key=lambda x: x["LastModified"],
                    reverse=True
                )[:num_files]
                
                local_files = []
                # Download each file
                for idx, item in enumerate(sorted_files):
                    local_filename = os.path.join(self.data_folder, f"radar_data_{idx}")
                    s3.download_file(bucket, item["Key"], local_filename)
                    print(f"Downloaded: {item['Key']} -> {local_filename}")
                    local_files.append(local_filename)
                
                return local_files
            else:
                print(f"No radar data found for {radar_id} at {file_prefix}")
                # Return existing files if available
                existing_files = [os.path.join(self.data_folder, f) for f in os.listdir(self.data_folder) 
                                 if os.path.isfile(os.path.join(self.data_folder, f)) 
                                 and f.startswith("radar_data_")]
                if existing_files:
                    print(f"Using {len(existing_files)} existing radar data files")
                    return sorted(existing_files)[:num_files]
                return None
                
        except Exception as e:
            print(f"Error fetching radar data: {e}")
            # Return existing files as fallback
            try:
                existing_files = [os.path.join(self.data_folder, f) for f in os.listdir(self.data_folder) 
                                if os.path.isfile(os.path.join(self.data_folder, f)) 
                                and f.startswith("radar_data_")]
                if existing_files:
                    print(f"Using {len(existing_files)} existing radar data files as fallback")
                    return sorted(existing_files)[:num_files]
            except:
                pass
            return None


if __name__ == "__main__":
    # Test the radar data service
    service = RadarDataService()
    files = service.fetch_nexrad_data("KDOX", 1)
    if files:
        print(f"Successfully downloaded {len(files)} radar data files")
        # Try loading the first file
        try:
            radar = pyart.io.read(files[0])
            print(f"Successfully loaded radar data with {radar.nsweeps} sweeps")
        except Exception as e:
            print(f"Error loading radar data: {e}")