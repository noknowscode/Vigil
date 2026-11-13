# ready.py <png> <kind> : exit 0 when a Today peek (CSS-pixel clip) shows its late-drawn parts
#   kind "orange": the clip holds Prep-orange timeline bars; kind "photo": the clip is a photo (not a flat placeholder)
import sys
from PIL import Image, ImageStat
im = Image.open(sys.argv[1]).convert('RGB')
kind = sys.argv[2]
if kind == 'orange':
    targets = [(201, 122, 46), (230, 167, 102)]
    n = sum(1 for p in im.get_flattened_data() if any(sum(abs(a - b) for a, b in zip(p, t)) < 40 for t in targets))
    print('orange px', n); sys.exit(0 if n > 30 else 1)
if kind == 'size':
    import os; n = os.path.getsize(sys.argv[1]); print('bytes', n); sys.exit(0 if n > 3000 else 1)
if kind == 'primary':  # the drawer's Record reading button: mostly Primary green (Light #244936, Dark #8FD1AE)
    targets = [(36, 73, 54), (143, 209, 174)]
    px = im.get_flattened_data(); n = sum(1 for p in px if any(sum(abs(a - b) for a, b in zip(p, t)) < 30 for t in targets))
    print('primary frac', round(n / len(px), 2)); sys.exit(0 if n / len(px) > .5 else 1)
if kind == 'photo':
    sd = sum(ImageStat.Stat(im).stddev) / 3
    print('stddev', round(sd, 1)); sys.exit(0 if sd > 18 else 1)
