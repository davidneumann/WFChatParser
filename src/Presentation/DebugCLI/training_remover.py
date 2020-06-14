# requires opencv2-python and tqdm (though i can easily remove tqdm)
# Made by gigapatches the wise
import sys
from os import path
import glob

import cv2
from tqdm import tqdm


for f in tqdm(glob.glob(sys.argv[1]), desc='images'):
    fname = path.basename(f)
    img = cv2.imread(f)
    img[:, 0:230] = (0, 0, 0)
    cv2.imwrite(f"{sys.argv[2]}/{fname}", img)