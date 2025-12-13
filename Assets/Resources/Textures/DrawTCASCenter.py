import matplotlib.pyplot as plt

# Create a figure and axis
fig, ax = plt.subplots()

# Draw the vertical line
ax.plot([0, 0], [0.3, -0.22], color='white', linewidth=5)

# Draw the horizontal line (arms)
ax.plot([-0.2, 0.2], [0.2, 0.2], color='white', linewidth=5)

# Draw the horizontal tail
ax.plot([-0.1, 0.1], [-0.2, -0.2], color='white', linewidth=5)

# Set the background to transparent
fig.patch.set_alpha(0)
ax.set_facecolor((0, 0, 0, 0))  # Transparent background

# Remove axes for a clean look
ax.axis('off')

# Set the aspect ratio to equal
ax.set_aspect('equal')

# Save the figure with a transparent background
plt.savefig("plane_symbol.png", dpi=300, transparent=True)

# Show the plot
plt.show()