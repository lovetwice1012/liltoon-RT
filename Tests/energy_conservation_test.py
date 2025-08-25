import random, math

def dot(a, b):
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]

def normalize(v):
    l = math.sqrt(dot(v, v))
    return (v[0]/l, v[1]/l, v[2]/l)

def evaluate_brdf(albedo, metallic, roughness, clearcoat, clearcoat_roughness, sheen, n, l, v):
    h = normalize((l[0]+v[0], l[1]+v[1], l[2]+v[2]))
    ndotl = max(0.0, dot(n, l))
    ndotv = max(0.0, dot(n, v))
    ndoth = max(0.0, dot(n, h))
    vdoth = max(0.0, dot(v, h))
    a = roughness*roughness
    a2 = a*a
    denom = ndoth*ndoth*(a2-1.0)+1.0
    D = a2/(math.pi*denom*denom + 1e-7)
    k = (roughness+1.0)
    k = (k*k)/8.0
    Gv = ndotv/(ndotv*(1.0-k)+k)
    Gl = ndotl/(ndotl*(1.0-k)+k)
    G = Gv*Gl
    F0 = 0.04*(1-metallic) + albedo*metallic
    F = F0 + (1-F0)*((1-vdoth)**5)
    spec = F*(D*G/(4*ndotv*ndotl + 1e-5))
    diffuse = albedo/math.pi*(1-metallic)
    if sheen > 0.0:
        diffuse += albedo*sheen*((1-vdoth)**5)*(1-metallic)
    clear = 0.0
    if clearcoat > 0.0:
        cc_rough = max(0.001, clearcoat_roughness*clearcoat_roughness)
        cc_a = cc_rough*cc_rough
        cc_denom = ndoth*ndoth*(cc_a-1.0)+1.0
        Dc = cc_a/(math.pi*cc_denom*cc_denom + 1e-7)
        kc = (cc_rough+1.0)
        kc = (kc*kc)/8.0
        Gvc = ndotv/(ndotv*(1.0-kc)+kc)
        Glc = ndotl/(ndotl*(1.0-kc)+kc)
        Gc = Gvc*Glc
        Fc = 0.04 + 0.96*((1-vdoth)**5)
        clear = clearcoat * (Fc * (Dc*Gc/(4*ndotv*ndotl + 1e-5)))
    return (diffuse + spec + clear) * ndotl

def sample_hemisphere():
    u1 = random.random()
    u2 = random.random()
    phi = 2*math.pi*u1
    cos_theta = u2
    sin_theta = math.sqrt(max(0.0, 1 - cos_theta*cos_theta))
    return (sin_theta*math.cos(phi), cos_theta, sin_theta*math.sin(phi))

if __name__ == "__main__":
    n = (0,1,0)
    v = (0,1,0)
    albedo = 1.0
    metallic = 0.5
    roughness = 0.5
    clearcoat = 0.2
    clearcoat_roughness = 0.1
    sheen = 0.3
    samples = 10000
    total = 0.0
    for _ in range(samples):
        l = sample_hemisphere()
        pdf = 1.0/(2*math.pi)
        brdf = evaluate_brdf(albedo, metallic, roughness, clearcoat, clearcoat_roughness, sheen, n, l, v)
        total += brdf / pdf
    print("Estimated reflectance:", total / samples)
