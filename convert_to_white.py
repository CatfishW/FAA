from PIL import Image
import os
import glob

def convert_to_white(directory):
    # Search for all png files in the directory
    search_path = os.path.join(directory, "*.png")
    files = glob.glob(search_path)
    
    if not files:
        print(f"No PNG files found in {directory}")
        return

    print(f"Found {len(files)} files. Starting conversion...")

    for file_path in files:
        try:
            with Image.open(file_path) as img:
                # Ensure it's in RGBA mode to handle transparency
                img = img.convert("RGBA")
                
                # Get data
                data = img.getdata()
                
                # Create a new list for the white version
                new_data = []
                for item in data:
                    # item is (R, G, B, A)
                    # We want to change R, G, B to 255 but keep A
                    new_data.append((255, 255, 255, item[3]))
                
                # Update image data
                img.putdata(new_data)
                
                # Save back to the same path
                img.save(file_path)
                print(f"Converted: {os.path.basename(file_path)}")
        except Exception as e:
            print(f"Failed to convert {file_path}: {e}")

if __name__ == "__main__":
    target_dir = r"z:\FAA\Assets\Resources\FFA GUI ASSETS\Iteration 2"
    convert_to_white(target_dir)
