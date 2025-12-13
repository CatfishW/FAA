import matplotlib.pyplot as plt
import numpy as np

def draw_circle_with_ticks(radius_nm, num_ticks):
    # Convert nautical miles to a generic unit (e.g., kilometers or arbitrary units)
    radius = radius_nm  # Assuming 1 NM = 1 unit for simplicity
    tick_length = 0.2  # Length of the ticks

    # Create the figure with a transparent background
    plt.figure(figsize=(6, 6), facecolor='none')
    ax = plt.gca()
    ax.set_facecolor('none')  # Transparent background

    # Add ticks on the top hemisphere
    for i in range(num_ticks):
        angle = (i / num_ticks) * np.pi  # Divide the top half into equal parts
        tick_x_start = radius * np.cos(angle)
        tick_y_start = radius * np.sin(angle)
        tick_x_end = (radius + tick_length) * np.cos(angle)
        tick_y_end = (radius + tick_length) * np.sin(angle)
        plt.plot([tick_x_start, tick_x_end], [tick_y_start, tick_y_end], color='white', linewidth=1.5)  # White tick lines

    # Add ticks on the bottom hemisphere
    for i in range(num_ticks):
        angle = np.pi + (i / num_ticks) * np.pi  # Divide the bottom half into equal parts
        tick_x_start = radius * np.cos(angle)
        tick_y_start = radius * np.sin(angle)
        tick_x_end = (radius + tick_length) * np.cos(angle)
        tick_y_end = (radius + tick_length) * np.sin(angle)
        plt.plot([tick_x_start, tick_x_end], [tick_y_start, tick_y_end], color='white', linewidth=1.5)  # White tick lines

    # Formatting
    plt.gca().set_aspect('equal', adjustable='box')
    plt.axis('off')  # Turn off the axes for a clean look
    plt.savefig('circle_with_ticks.png', dpi=300, transparent=True)  # Save with transparent background
    plt.show()

# Draw the circle with a 2NM radius and 7 ticks on each hemisphere
draw_circle_with_ticks(radius_nm=2, num_ticks=7)