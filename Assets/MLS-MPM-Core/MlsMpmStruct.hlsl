#ifdef MLS_MPM_STRUCT_INCLUDED
#define MLS_MPM_STRUCT_INCLUDED

struct MpmParticle {
	float3 position;
	float3 velocity;
    float mass;
    float volume;
    float3x3 C; //C = D * B at Affine particle in cell
    float Jp
};

struct MpmCell
{
    float mass;
    float3 mass_x_velocity;
    float3 velocity;
    float3 force;
};

#endif