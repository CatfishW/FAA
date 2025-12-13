import matplotlib.pyplot as plt
import matplotlib.patches as patches

# Define colors
cyan_color = '#00C8FF'
yellow_color = '#FFD700'
red_color = '#FF3A33'

# Function to create a figure for a specific symbol
def create_symbol_figure(symbol_func, filename, title):
    fig, ax = plt.subplots(figsize=(4, 4), facecolor='#f0f0f8')
    ax.set_facecolor('#f8f8ff')
    ax.set_xlim(-2, 2)
    ax.set_ylim(-2, 2)
    ax.axis('off')
    symbol_func(ax)
    ax.set_title(title, fontsize=14, fontweight='bold', y=0.9)
    plt.savefig(filename, dpi=300, bbox_inches='tight',transparent=True)  # Save with transparent background
    plt.close(fig)

# Function to draw "Other Traffic" symbol
def draw_other_traffic(ax):
    diamond_size = 0.8
    other_traffic = patches.Polygon(
        [
            [-diamond_size, 0], 
            [0, diamond_size], 
            [diamond_size, 0], 
            [0, -diamond_size]
        ], 
        closed=True, edgecolor=cyan_color, facecolor='none', linewidth=3
    )
    ax.add_patch(other_traffic)

# Function to draw "Proximate Traffic" symbol
def draw_proximate_traffic(ax):
    diamond_size = 0.8
    proximate_traffic = patches.Polygon(
        [
            [-diamond_size, 0], 
            [0, diamond_size], 
            [diamond_size, 0], 
            [0, -diamond_size]
        ], 
        closed=True, edgecolor=cyan_color, facecolor=cyan_color, linewidth=2, alpha=0.9
    )
    ax.add_patch(proximate_traffic)

# Function to draw "Intruding Traffic" symbol
def draw_intruding_traffic(ax):
    intruding_traffic = patches.Circle(
        (0, 0), radius=0.7, edgecolor='black', 
        facecolor=yellow_color, linewidth=1.5, alpha=0.9
    )
    ax.add_patch(intruding_traffic)

# Function to draw "Threat" symbol
def draw_threat(ax):
    square_size = 1.2
    threat = patches.Rectangle(
        (-square_size / 2, -square_size / 2), 
        square_size, square_size, 
        edgecolor='black', facecolor=red_color, linewidth=1.5, alpha=0.9
    )
    ax.add_patch(threat)

# Create and save individual figures
create_symbol_figure(draw_other_traffic, 'other_traffic.png', None)
create_symbol_figure(draw_proximate_traffic, 'proximate_traffic.png', None)
create_symbol_figure(draw_intruding_traffic, 'intruding_traffic.png', None)
create_symbol_figure(draw_threat, 'threat.png', None)