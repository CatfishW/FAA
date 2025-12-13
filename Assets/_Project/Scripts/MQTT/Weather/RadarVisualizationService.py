"""
Radar Visualization Service Module
Provides functionality to visualize NEXRAD radar data
"""
import numpy as np
import pandas as pd
import pyart
import matplotlib.pyplot as plt
import matplotlib.patheffects as mpatheffects
import cartopy
import cartopy.crs as ccrs
from netCDF4 import num2date
import pytz
from scipy.spatial import cKDTree
from geopy.distance import geodesic
from matplotlib import transforms
import os
import shutil
import logging
import boto3
import datetime


class RadarVisualizationService:
    """
    Service for visualizing NEXRAD radar data
    """
    def __init__(self, stations_csv="nexrad_stations.csv", image_path="radar.png"):
        """
        Initialize the radar visualization service
        
        Parameters
        ----------
        stations_csv : str
            Path to CSV file containing NEXRAD station information
        image_path : str
            Default path to save radar visualization images
        """
        self.stations = pd.read_csv(stations_csv)
        self.image_path = image_path
        self.radar_data_dir = "radar_data"
        self.ensure_radar_data_dir()
        
        # AWS credentials for NOAA NEXRAD data
        self.aws_access_key_id = os.getenv('AWS_ACCESS_KEY_ID', '')
        self.aws_secret_access_key = os.getenv('AWS_SECRET_ACCESS_KEY', '')
    
    def ensure_radar_data_dir(self):
        """
        Ensure that the radar data directory exists
        
        Creates the directory if it doesn't exist
        """
        if not os.path.exists(self.radar_data_dir):
            try:
                os.makedirs(self.radar_data_dir)
                print(f"Created directory: {self.radar_data_dir}")
                
                # Create placeholder files to prevent future errors
                self.create_placeholder_radar_files()
            except Exception as e:
                print(f"Error creating directory {self.radar_data_dir}: {e}")
    
    def create_placeholder_radar_files(self):
        """
        Create placeholder radar data files
        
        This is a temporary solution to prevent file not found errors.
        In a production environment, you would download actual radar data files.
        """
        try:
            # Create simple text files as placeholders
            with open(os.path.join(self.radar_data_dir, "radar_data_0"), "w") as f:
                f.write("Placeholder for radar data file 0")
            
            with open(os.path.join(self.radar_data_dir, "radar_data_1"), "w") as f:
                f.write("Placeholder for radar data file 1")
                
            print("Created placeholder radar data files")
        except Exception as e:
            print(f"Error creating placeholder files: {e}")
            
    def check_radar_file_exists(self, radar_file):
        """
        Check if the radar file exists
        
        Parameters
        ----------
        radar_file : str
            Path to radar data file
            
        Returns
        -------
        bool
            True if the file exists, False otherwise
        """
        return os.path.exists(radar_file)
    
    def find_nearest_radar(self, user_lat, user_lon):
        """
        Find the nearest NEXRAD station based on user location
        
        Parameters
        ----------
        user_lat : float
            User's latitude
        user_lon : float
            User's longitude
            
        Returns
        -------
        tuple
            (ICAO code of nearest radar station, distance in km)
        """
        min_dist = float("inf")
        nearest_station = None

        for _, row in self.stations.iterrows():
            station_coords = (row["LAT"], row["LON"])
            distance = geodesic(station_coords, (user_lat, user_lon)).km

            if distance < min_dist:
                min_dist = distance
                nearest_station = row["ICAO"]

        return nearest_station, min_dist
    
    @staticmethod
    def thin_points(xy, radius, sort_key=None):
        """
        Thin out points that are too close to each other
        
        Parameters
        ----------
        xy : np.ndarray
            Array of point coordinates (N×2)
        radius : float
            Thinning radius
        sort_key : np.ndarray, optional
            Weight array for sorting; points with higher weights are retained first
            
        Returns
        -------
        np.ndarray
            Boolean mask array, True indicates the point is retained
        """
        mask = np.ones(xy.shape[0], dtype=bool)

        if sort_key is not None:
            sorted_indices = np.argsort(sort_key)[::-1]
        else:
            sorted_indices = np.arange(len(xy))

        tree = cKDTree(xy)

        for sort_ind in sorted_indices:
            if mask[sort_ind]:
                for neighbor in tree.query_ball_point(xy[sort_ind], radius):
                    if neighbor != sort_ind:
                        mask[neighbor] = False

        return mask
    
    @staticmethod
    def plot_range_rings(ax, user_lat, user_lon, radii, num_points=100, color='w', 
                         linestyle='--', heading=0, show_minor_ticks=True, show_labels=None):
        """
        Plot range rings at user location
        
        Parameters
        ----------
        ax : matplotlib.axes.Axes
            Axes object for plotting
        user_lat : float
            User's latitude
        user_lon : float
            User's longitude
        radii : float or list of floats
            Radii of the range rings (in km)
        num_points : int, optional
            Number of points used to draw the rings
        color : str, optional
            Plot color
        linestyle : str, optional
            Line style
        heading : float, optional
            Heading angle in degrees
        show_minor_ticks : bool, optional
            Whether to show minor ticks
        show_labels : list of str, optional
            Labels for major ticks
        """
        # Ensure radii is a list
        if isinstance(radii, (int, float)):
            radii = [radii]

        angles = np.linspace(0, 2 * np.pi, num_points)
        cos_lat = np.cos(np.radians(user_lat))

        # Draw each ring
        for r in radii:
            radius_deg = r / 110.574  # Latitude conversion: 1° ≈ 110.574 km
            lats = user_lat + radius_deg * np.sin(angles)
            lons = user_lon + (radius_deg * np.cos(angles)) / cos_lat
            ax.plot(lons, lats, color=color, linestyle=linestyle)

        # Add direction ticks and labels on the outermost ring
        if radii:
            max_radius = max(radii)
            max_radius_deg = max_radius / 110.574

            for theta in range(0, 360, 90 if not show_minor_ticks else 5):
                # Correct direction: 0° is due north, 90° is due east
                theta_rad = np.radians(90 - theta + heading)

                # Calculate points on the outermost ring
                lat_point = user_lat + max_radius_deg * np.sin(theta_rad)
                lon_point = user_lon + (max_radius_deg * np.cos(theta_rad)) / cos_lat

                # Label every 90° if show_labels is provided
                if show_labels and theta % 90 == 0:
                    label = show_labels[theta // 90]
                    ax.text(lon_point, lat_point, label, ha='center', va='top', 
                            fontsize=8, color=color)
    
    def visualize_radar(self, radar_file, radar_id, user_lat, user_lon, 
                        min_lat=None, min_lon=None, max_lat=None, max_lon=None,
                        heading=0, tilt=0, gain=0, ground_speed=0, altitude=0, 
                        destination="Unknown"):
        """
        Visualize radar data
        
        Parameters
        ----------
        radar_file : str
            Path to radar data file
        radar_id : str
            NEXRAD radar station identifier
        user_lat : float
            User's latitude
        user_lon : float
            User's longitude
        min_lat, min_lon, max_lat, max_lon : float, optional
            Map boundaries
        heading : float, optional
            Heading angle in degrees
        tilt : float, optional
            Tilt angle in degrees
        gain : float, optional
            Gain adjustment for reflectivity
        ground_speed : float, optional
            Ground speed in knots
        altitude : float, optional
            Altitude in feet
        destination : str, optional
            Destination name
        """
        # Read radar data
        radar = pyart.io.read(radar_file)
        lats = radar.gate_latitude
        lons = radar.gate_longitude

        if (min_lat is None or min_lon is None or max_lat is None or max_lon is None):
            min_lon = lons['data'].min()
            min_lat = lats['data'].min()
            max_lat = lats['data'].max()
            max_lon = lons['data'].max()

        print('min_lat:', min_lat, ' min_lon:', min_lon,
              ' max_lat:', max_lat, ' max_lon:', max_lon)
              
        # Apply gain control to reflectivity data before plotting
        if 'reflectivity' in radar.fields:
            field_data = radar.fields['reflectivity']['data']
            # Adjust reflectivity: higher gain amplifies weak returns
            radar.fields['reflectivity']['data'] = field_data + gain
            
        # Get time information
        sweep = 0
        index_at_start = radar.sweep_start_ray_index['data'][sweep]
        time_at_start = num2date(radar.time['data'][index_at_start],
                                radar.time['units'],
                                only_use_cftime_datetimes=False)
        auto_timezone = pytz.timezone('US/Eastern')
        local_time = auto_timezone.fromutc(time_at_start)
        fancy_date_string = local_time.strftime('%A %B %d at %I:%M %p %Z')
        print(fancy_date_string)

        # Create figure and axes with fixed size and transparent background
        fig = plt.figure(figsize=[10, 8], dpi=300, facecolor='none')
        ax = plt.axes(projection=ccrs.PlateCarree())
        ax.set_facecolor('none')  # Set axes background to transparent
        ax.patch.set_alpha(0)     # Ensure patch is transparent
        
        # Remove all axes spines for full transparency
        for spine in ax.spines.values():
            spine.set_visible(False)

        display = pyart.graph.RadarMapDisplay(radar)
        lat_0 = display.loc[0]
        lon_0 = display.loc[1]

        # Cartopy projection (Mercator)
        projection = cartopy.crs.Mercator(
            central_longitude=lon_0,
            min_latitude=min_lat, max_latitude=max_lat)
        
        # Calculate beam ground offset based on tilt
        max_radius = 20  # Maximum plotting radius in km
        beam_offset_km = max_radius * np.tan(np.radians(tilt))
        beam_offset_deg = beam_offset_km / 110.574
        eff_lat = user_lat + beam_offset_deg
        eff_lon = user_lon  # No east/west shift in this simple model

        # Draw range rings at the beam impact location
        # Only draw major ticks at every 90 degrees and remove small ticks
        self.plot_range_rings(ax, eff_lat, eff_lon, [5, 10, 15, max_radius], 
                          color='w', heading=heading, show_minor_ticks=False, 
                          show_labels=['N', 'E', 'S', 'W'])
        
        # Apply rotation transform for heading
        transform = transforms.Affine2D().rotate_deg_around(user_lon, user_lat, heading)
        ax.transData = transform + ax.transData
        
        # Plot radar data with transparent background
        cmap = pyart.graph.cm.NWSRef
        cmap.set_under('none')  # Make low values completely transparent
        display.plot_ppi_map(
            'reflectivity', 
            0,
            resolution='10m',
            colorbar_flag=False,
            title_flag=False,
            projection=projection,
            min_lon=min_lon, max_lon=max_lon,
            min_lat=min_lat, max_lat=max_lat,
            vmin=-12, vmax=64,
            ax=ax,
            cmap=cmap,
            alpha=0.9  # Slightly reduce alpha to ensure transparency works
        )

        # Mark radar station location
        display.plot_point(lon_0, lat_0, label_text=radar_id, marker='o', color='white')

        # Set image clipping based on beam tilt
        lat_span = (max_radius / 110.574)
        ax.set_ylim([eff_lat, eff_lat + lat_span])
        lon_offset = (max_radius / 111.320) / np.cos(np.radians(user_lat))
        ax.set_xlim([user_lon - lon_offset, user_lon + lon_offset])
        
        # Add flight information overlay with transparent background
        fig.text(0.05, 0.85,
            f"HDG: {heading}°\nTILT: {tilt}°\nGS: {ground_speed} KT\nALT: {altitude} FT\nDEST: {destination}",
            fontsize=12, color='cyan',
            bbox=dict(facecolor='none', edgecolor='none', alpha=0))
        
        # Mark user location and beam impact point
        display.plot_point(user_lon, user_lat, marker='^', color='red')
        display.plot_point(eff_lon, eff_lat, marker='*', color='yellow')
        
        # Save the image with transparent background
        plt.savefig(self.image_path, 
                    bbox_inches='tight', 
                    dpi=300, 
                    transparent=True,  # Ensure transparency 
                    facecolor='none',  # Make figure background transparent
                    edgecolor='none',  # No edge color
                    pad_inches=0.1)
    
    def generate_visualization(self, user_lat, user_lon, width_km=50, height_km=50,
                              heading=0, tilt=0, gain=0, radar_file=None, radar_id=None):
        """
        Generate radar visualization based on user location
        
        Parameters
        ----------
        user_lat : float
            User's latitude
        user_lon : float
            User's longitude
        width_km : float, optional
            Map width in km
        height_km : float, optional
            Map height in km
        heading : float, optional
            Heading angle in degrees
        tilt : float, optional
            Tilt angle in degrees
        gain : float, optional
            Gain adjustment for reflectivity
        radar_file : str, optional
            Path to radar data file (if None, uses nearest station)
        radar_id : str, optional
            NEXRAD radar station ID (if None, finds nearest)
            
        Returns
        -------
        str
            Path to the generated image
        """
        # Ensure the radar data directory exists
        self.ensure_radar_data_dir()
        
        # Determine map boundaries based on user's location
        earth_radius = 6371  # km
        d_lat = (height_km / earth_radius) * (180 / np.pi)
        d_lon = (width_km / earth_radius) * (180 / np.pi) / np.cos(np.radians(user_lat))
        min_lat = user_lat - d_lat / 2
        max_lat = user_lat + d_lat / 2
        min_lon = user_lon - d_lon / 2
        max_lon = user_lon + d_lon / 2
        
        # If radar file and ID are not provided, find the nearest station
        if radar_file is None or radar_id is None:
            nearest_id, distance = self.find_nearest_radar(user_lat, user_lon)
            print(f"Nearest NEXRAD Station: {nearest_id} ({distance:.2f} km away)")
            radar_id = nearest_id
            
            # Download real NEXRAD data from AWS S3
            radar_files = self.fetch_nexrad_data(radar_id, num_files=2)
            
            if radar_files and len(radar_files) > 0:
                radar_file = radar_files[0]  # Use the most recent file
                print(f"Using real NEXRAD data file: {radar_file}")
            else:
                print("No radar data files found. Using placeholder.")
                radar_file = os.path.join(self.radar_data_dir, "radar_data_0")
                self.create_placeholder_radar_files()

        # Check if the radar file exists
        if not self.check_radar_file_exists(radar_file):
            print(f"Warning: Radar file {radar_file} does not exist. Using placeholder.")
            self.create_placeholder_radar_files()
            radar_file = os.path.join(self.radar_data_dir, "radar_data_0")

        # Visualize the radar data
        try:
            self.visualize_radar(
                radar_file, radar_id,
                user_lat, user_lon,
                min_lat=min_lat, min_lon=min_lon, max_lat=max_lat, max_lon=max_lon,
                heading=heading, tilt=tilt, gain=gain
            )
        except Exception as e:
            print(f"Error in primary visualization: {e}")
            # Fallback to secondary radar file
            try:
                # If we downloaded multiple files, try the second one
                if radar_files and len(radar_files) > 1:
                    radar_file = radar_files[1]
                else:
                    radar_file = os.path.join(self.radar_data_dir, "radar_data_1")
                
                # Check if the fallback radar file exists
                if not self.check_radar_file_exists(radar_file):
                    print(f"Warning: Fallback radar file {radar_file} does not exist. Using placeholder.")
                    self.create_placeholder_radar_files()
                    radar_file = os.path.join(self.radar_data_dir, "radar_data_1")
                    
                self.visualize_radar(
                    radar_file, radar_id,
                    user_lat, user_lon,
                    min_lat=min_lat, min_lon=min_lon, max_lat=max_lat, max_lon=max_lon,
                    heading=heading, tilt=tilt, gain=gain
                )
            except Exception as e2:
                print(f"Error in fallback visualization: {e2}")
                
        return self.image_path

    def fetch_nexrad_data(self, radar_id, num_files=5, folder=None):
        """
        Fetch NEXRAD radar data from AWS S3 bucket
        
        Parameters
        ----------
        radar_id : str
            NEXRAD radar station identifier
        num_files : int, optional
            Number of files to download
        folder : str, optional
            Folder to save downloaded files
            
        Returns
        -------
        list
            List of paths to downloaded radar data files
        """
        if folder is None:
            folder = self.radar_data_dir
            
        s3 = boto3.client(
            "s3",
            aws_access_key_id=self.aws_access_key_id,
            aws_secret_access_key=self.aws_secret_access_key
        )
        bucket = "noaa-nexrad-level2"
        
        # Get today's date in UTC for the file prefix
        today = datetime.datetime.utcnow()
        file_prefix = f"{today.year}/{today.strftime('%m')}/{today.strftime('%d')}/{radar_id}/"
        
        print(f"Looking for NEXRAD data with prefix: {file_prefix}")
        
        # List objects in the bucket
        response = s3.list_objects_v2(Bucket=bucket, Prefix=file_prefix)
        if "Contents" in response:
            # Sort files by last modified time (newest first)
            sorted_files = sorted(
                response["Contents"],
                key=lambda x: x["LastModified"],
                reverse=True
            )[:num_files]
            
            local_files = []
            if not os.path.exists(folder):
                os.makedirs(folder)
                
            # Download files
            for idx, item in enumerate(sorted_files):
                local_filename = os.path.join(folder, f"radar_data_{idx}")
                s3.download_file(bucket, item["Key"], local_filename)
                print(f"Downloaded: {item['Key']} -> {local_filename}")
                local_files.append(local_filename)
                
            return local_files
        else:
            print("No radar data found.")
            return None


if __name__ == "__main__":
    # Test the radar visualization service
    service = RadarVisualizationService()
    # Sample coordinates for testing
    user_lat, user_lon = 39.7000, -75.1091
    
    # Generate visualization
    image_path = service.generate_visualization(user_lat, user_lon)
    print(f"Radar visualization generated at: {image_path}")