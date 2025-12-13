import json
import os
import math
import argparse
import time
from datetime import datetime

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.animation import FFMpegWriter

def haversine_km(lat1, lon1, lat2, lon2):
    R = 6371.0
    dlat = math.radians(lat2 - lat1)
    dlon = math.radians(lon2 - lon1)
    a = math.sin(dlat/2)**2 + math.cos(math.radians(lat1)) * math.cos(math.radians(lat2)) * math.sin(dlon/2)**2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    return R * c

def load_jsonl(path):
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            yield json.loads(line)

def main():
    parser = argparse.ArgumentParser(description='Render traffic logs jsonl to mp4.')
    parser.add_argument('input', help='Path to jsonl log file')
    parser.add_argument('--output', default=None, help='Output mp4 path (default next to input)')
    parser.add_argument('--fps', type=int, default=5)
    args = parser.parse_args()

    if args.output is None:
        base, _ = os.path.splitext(args.input)
        args.output = base + '.mp4'

    frames = list(load_jsonl(args.input))
    if not frames:
        print('No frames found')
        return

    # Determine map extents
    lats = []
    lons = []
    for fr in frames:
        for a in fr.get('aircraft', []):
            if a['latitude'] and a['longitude']:
                lats.append(a['latitude'])
                lons.append(a['longitude'])
    if not lats or not lons:
        print('No positions in log')
        return

    lat_min, lat_max = min(lats), max(lats)
    lon_min, lon_max = min(lons), max(lons)
    # padding
    lat_pad = (lat_max - lat_min) * 0.1 or 0.5
    lon_pad = (lon_max - lon_min) * 0.1 or 0.5
    lat_min -= lat_pad; lat_max += lat_pad
    lon_min -= lon_pad; lon_max += lon_pad

    fig, ax = plt.subplots(figsize=(8, 6))
    ax.set_xlim(lon_min, lon_max)
    ax.set_ylim(lat_min, lat_max)
    ax.set_xlabel('Longitude')
    ax.set_ylabel('Latitude')
    ax.set_title('Air Traffic Log Playback')

    metadata = dict(title='Traffic Playback', artist='visualize_traffic_log.py')
    writer = FFMpegWriter(fps=args.fps, metadata=metadata)

    with writer.saving(fig, args.output, dpi=150):
        for fr in frames:
            ax.cla()
            ax.set_xlim(lon_min, lon_max)
            ax.set_ylim(lat_min, lat_max)
            ts = fr.get('time', 0)
            dt = datetime.utcfromtimestamp(ts).strftime('%Y-%m-%d %H:%M:%S UTC')
            ax.set_title(f'Air Traffic at {dt}  (n={fr.get("count", 0)})')

            ac = fr.get('aircraft', [])
            xs = [a['longitude'] for a in ac]
            ys = [a['latitude'] for a in ac]
            ax.scatter(xs, ys, s=10, c='tab:blue', alpha=0.8)
            # show some labels
            for a in ac[:50]:
                ax.text(a['longitude'], a['latitude'], a.get('callsign') or a['icao24'], fontsize=6, color='black')

            writer.grab_frame()

    print('Saved to', args.output)

if __name__ == '__main__':
    main()


