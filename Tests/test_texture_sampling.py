import math
import pytest


def sample_color(pixels, width, height, u, v):
    u = u % 1.0
    v = v % 1.0
    x = u * (width - 1)
    y = v * (height - 1)
    x0 = int(math.floor(x))
    y0 = int(math.floor(y))
    x1 = min(x0 + 1, width - 1)
    y1 = min(y0 + 1, height - 1)
    tx = x - x0
    ty = y - y0
    def get(ix, iy):
        r, g, b = pixels[iy * width + ix]
        return (r, g, b)
    c00 = get(x0, y0)
    c10 = get(x1, y0)
    c01 = get(x0, y1)
    c11 = get(x1, y1)
    c0 = tuple(c00[i]*(1-tx) + c10[i]*tx for i in range(3))
    c1 = tuple(c01[i]*(1-tx) + c11[i]*tx for i in range(3))
    return tuple(c0[i]*(1-ty) + c1[i]*ty for i in range(3))


def test_sample_color_center():
    pixels = [
        (0.0, 0.0, 0.0),
        (1.0, 0.0, 0.0),
        (0.0, 1.0, 0.0),
        (0.0, 0.0, 1.0),
    ]  # 2x2 texture
    color = sample_color(pixels, 2, 2, 0.5, 0.5)
    assert color == pytest.approx((0.25, 0.25, 0.25))
