using System;
using SimplexNoise;

namespace Enviro3D
{
	public class NoiseWrapper
	{ 
		public bool add = true;
		float y_scale;
		float gen_scale;
		float seed;
		public NoiseWrapper (float y_scale, float gen_scale, bool add, float seed)
		{
			this.y_scale = y_scale;
			this.gen_scale = gen_scale;
			this.seed = seed;
			this.add = add;
		}

		public NoiseWrapper (float y_scale, float gen_scale, bool add)
		{
			this.y_scale = y_scale;
			this.gen_scale = gen_scale;
			Random r = new Random();
			this.seed = (float)r.NextDouble() * 100;
			this.add = add;
		}

		public float getNoise(float x, float y)
		{
			return 1*y_scale + Noise.Generate(x * gen_scale + seed, y * gen_scale + seed) * y_scale;
		}
	}
}

