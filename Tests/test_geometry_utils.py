import math
import pytest


def _sub(a, b):
    return (a[0]-b[0], a[1]-b[1], a[2]-b[2])

def _cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])

def _dot(a,b):
    return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]

def _normalize(v):
    l = math.sqrt(_dot(v,v))
    return (v[0]/l, v[1]/l, v[2]/l)

def calculate_normals(verts, indices):
    norms = [(0.0,0.0,0.0) for _ in verts]
    norms = [list(n) for n in norms]
    for i in range(0, len(indices), 3):
        i0, i1, i2 = indices[i:i+3]
        v0, v1, v2 = verts[i0], verts[i1], verts[i2]
        n = _cross(_sub(v1,v0), _sub(v2,v0))
        for idx in (i0,i1,i2):
            norms[idx][0]+=n[0]; norms[idx][1]+=n[1]; norms[idx][2]+=n[2]
    return [_normalize(n) for n in norms]

def calculate_tangents(verts, uvs, indices, norms):
    tan1 = [[0.0,0.0,0.0] for _ in verts]
    tan2 = [[0.0,0.0,0.0] for _ in verts]
    for i in range(0, len(indices), 3):
        i0,i1,i2 = indices[i:i+3]
        v0,v1,v2 = verts[i0], verts[i1], verts[i2]
        w0,w1,w2 = uvs[i0], uvs[i1], uvs[i2]
        x1,y1,z1 = v1[0]-v0[0], v1[1]-v0[1], v1[2]-v0[2]
        x2,y2,z2 = v2[0]-v0[0], v2[1]-v0[1], v2[2]-v0[2]
        s1,s2 = w1[0]-w0[0], w2[0]-w0[0]
        t1,t2 = w1[1]-w0[1], w2[1]-w0[1]
        r = 1.0/((s1*t2 - s2*t1) + 1e-8)
        sdir = ((t2*x1 - t1*x2)*r, (t2*y1 - t1*y2)*r, (t2*z1 - t1*z2)*r)
        tdir = ((s1*x2 - s2*x1)*r, (s1*y2 - s2*y1)*r, (s1*z2 - s2*z1)*r)
        for idx in (i0,i1,i2):
            tan1[idx][0]+=sdir[0]; tan1[idx][1]+=sdir[1]; tan1[idx][2]+=sdir[2]
            tan2[idx][0]+=tdir[0]; tan2[idx][1]+=tdir[1]; tan2[idx][2]+=tdir[2]
    tangents = []
    for i in range(len(verts)):
        n = norms[i]
        t = tan1[i]
        dot_nt = _dot(n,t)
        tangent = _normalize((t[0]-n[0]*dot_nt, t[1]-n[1]*dot_nt, t[2]-n[2]*dot_nt))
        cross_nt = _cross(n, t)
        sign = -1.0 if _dot(cross_nt, tan2[i]) < 0.0 else 1.0
        tangents.append((tangent[0], tangent[1], tangent[2], sign))
    return tangents

def test_calculate_normals_quad():
    verts = [(0,0,0),(1,0,0),(1,0,1),(0,0,1)]
    indices = [0,1,2,0,2,3]
    normals = calculate_normals(verts, indices)
    for n in normals:
        assert (n[0], n[2]) == pytest.approx((0,0))
        assert abs(n[1]) == pytest.approx(1.0)

def test_calculate_tangents_quad():
    verts = [(0,0,0),(1,0,0),(1,0,1),(0,0,1)]
    indices = [0,1,2,0,2,3]
    uvs = [(0,0),(1,0),(1,1),(0,1)]
    norms = calculate_normals(verts, indices)
    tangents = calculate_tangents(verts, uvs, indices, norms)
    for t in tangents:
        assert (t[0],t[1],t[2]) == pytest.approx((1,0,0))
        assert t[3] == pytest.approx(1.0)
