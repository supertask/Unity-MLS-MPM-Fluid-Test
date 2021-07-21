#ifndef __RANDOM_UTILL__
#define __RANDOM_UTILL__

float gold_noise(float2 coordinate, float seed) {
	return frac(tan(
		distance(coordinate * (seed + GOLDEN_RATIO), float2(GOLDEN_RATIO, PI))
	) * SQ2);
}

float3 randomPosOnSphere(float index, float seed)
{
	// generating uniform points on the sphere: http://corysimon.github.io/articles/uniformdistn-on-sphere/
	float fi = float(index);
	// Note: Use uniform random generator instead of noise in your applications
	float theta = 2.0f * PI * gold_noise(float2(fi * 0.3482f, fi * 2.18622f), seed);
	float phi = acos(1.0f - 2.0f * gold_noise(float2(fi * 1.9013, fi * 0.94312), seed));

	// NOTE(Tasuku): If an amount of cell index "0" is too big, 
	// increase a value of EPISILON to avoid unexpected (x,y,z).
	// https://github.com/supertask/Unity-MLS-MPM-Fluid-Test/issues/3
	theta = clamp(theta, EPSILON, 2 * PI - EPSILON);
	phi = clamp(phi, EPSILON, 2 * PI - EPSILON);

	float x = sin(phi) * cos(theta);
	float y = sin(phi) * sin(theta);
	float z = cos(phi);

	return float3(x, y, z);
}

inline float random(float2 _st) {
	return frac(sin(dot(_st.xy,
		float2(12.9898, 78.233))) *
		43758.5453123);
}

inline float3 random3D(float2 p) {
	return 2.0 * (float3(random(p * 1), random(p * 2), random(p * 3)) - 0.5);
}


#endif
