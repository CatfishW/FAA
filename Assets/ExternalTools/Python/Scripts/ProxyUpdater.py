import requests
import os

def download_csv_file(url, save_path):
    """
    Downloads a CSV file from the given URL and saves it to the specified path.
    """
    try:
        response = requests.get(url)
        response.raise_for_status()  # Raise an error for bad status codes

        # Save the content to a file
        with open(save_path, 'wb') as file:
            file.write(response.content)
        print(f"File downloaded successfully: {save_path}")
    except requests.exceptions.RequestException as e:
        print(f"Failed to download the file: {e}")

if __name__ == "__main__":
    # URL of the .csv file to download
    csv_url = "https://raw.githubusercontent.com/proxifly/free-proxy-list/main/proxies/protocols/socks4/data.csv"
    
    # Local path to save the downloaded file
    save_directory = "z:\\unity\\FAA_symbology_unity_project\\Assets\\Scripts"
    os.makedirs(save_directory, exist_ok=True)  # Create directory if it doesn't exist
    save_path = os.path.join(save_directory, "data.csv")
    
    # Download the file
    download_csv_file(csv_url, save_path)