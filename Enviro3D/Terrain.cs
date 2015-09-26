using System;
using System.Drawing;
using SimplexNoise;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;
using System.Collections.Generic;

namespace Enviro3D
{
	public class Terrain
	{

		public struct TerrainBlock
		{
			public float water;
			public float porosity;
			public float slope;

			//tl, tr, bl, br
			public TerrainBlock(float porosity, float v1, float v2, float v3, float v4)
			{
				water = 0;
				float[] vertices = {v1,v2,v3,v4};

				int maxi = 0;
				int mini = 0;
				float max = -float.MaxValue;
				float min = float.MaxValue;

				for (int i = 0; i < vertices.Length; i++) {
					if (vertices[i] > max) {
						max = vertices[i];
						maxi = i;
					}

					if (vertices[i] < min) {
						min = vertices[i];
						mini = i;
					}
				}

				//why is this the only mechanism i can think of... herp derp
				if ((mini == 3 && maxi == 0) || (mini == 0 && maxi == 3) || (mini == 1 && maxi == 2) || (mini == 2 && maxi == 1)) //diagonal
					slope = (max - min)/1.41421356237309504880f; //sqrt 2			
				else
					slope = max - min ;

				this.porosity = 1/slope;			
			}
		}
			

		TerrainBlock[,] terrain_blocks;
		List<NoiseWrapper> noise_gen;
		float[,] terrain_vertices;
		float scale;
		public Terrain (int width, int height, float scale, List<NoiseWrapper> noisy)
		{
			terrain_vertices = new float[width + 1,height + 1];
			terrain_blocks = new TerrainBlock[width, height];
			this.scale = scale;
			noise_gen = noisy;

		}

		public void GenerateTerrainFromNoise()
		{
			int width = terrain_vertices.GetLength(0);
			int height = terrain_vertices.GetLength(1);	

	

			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {					
					foreach (var item in noise_gen) {
						if (item.add)
							terrain_vertices[i,j] += item.getNoise(i,j);
						else
							terrain_vertices[i,j] *= item.getNoise(i,j);
					}				

					if (i > 0 && j > 0)
					{
						//make and store info about terrain from vertices
						terrain_blocks[i-1,j-1] = new TerrainBlock(5, terrain_vertices[i-1, j-1], 
							terrain_vertices[i-1, j],
							terrain_vertices[i, j],
							terrain_vertices[i, j-1]);
					}
				}
			}
		}

		public void Draw()
		{
			Color4 terrain_ambient_colour = new Color4(0.1f  , 0.1f  , 0.1f , 1f);
			Color4 terrain_diffuse_colour = new Color4(0.2f   , 0.6f , 0.2f  , 1f);

			int width = terrain_vertices.GetLength(0) - 1;
			int height = terrain_vertices.GetLength(1) - 1;

			int wall_height = 30;

			GL.Begin(PrimitiveType.Triangles);
			GL.ShadeModel(ShadingModel.Smooth);
			//draw terrain
			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {
					if (terrain_blocks[i,j].slope > 6f)
					{
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, terrain_ambient_colour);
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  terrain_diffuse_colour);
					}
					else
					{
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 0.3f));
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.7f  , 0.2f  , 1f));
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(0.0f   , 0.15f  , 0.0f  , 1f));
					}
					Vector3 bl = new Vector3(i * scale, terrain_vertices[i,j], j * -scale);
					Vector3 br = new Vector3(i * scale, terrain_vertices[i,j+1], (j+1) * -scale);
					Vector3 tr = new Vector3((i+1) * scale, terrain_vertices[i+1,j+1], (j+1) * -scale);
					Vector3 tl = new Vector3((i+1) * scale, terrain_vertices[i+1,j ], j * -scale);

					//draw the triangles with the shortest diagonal line through quad
					if ((bl - tr).LengthSquared < (tl - br).LengthSquared)
					{
						GL.Normal3(Vector3.Cross(tr-bl, tl - bl).Normalized());
						GL.Vertex3(bl);
						GL.Vertex3(tl);
						GL.Vertex3(tr);
						GL.Normal3(Vector3.Cross(br-bl, tr - bl).Normalized());
						GL.Vertex3(bl);
						GL.Vertex3(tr);
						GL.Vertex3(br);
					}
					else
					{
						GL.Normal3(Vector3.Cross(tr-bl, tl - bl).Normalized());
						GL.Vertex3(bl);
						GL.Vertex3(tl);
						GL.Vertex3(br);
						GL.Normal3(Vector3.Cross(br-bl, tr - bl).Normalized());
						GL.Vertex3(tl);
						GL.Vertex3(tr);
						GL.Vertex3(br);
					}

				}
			}
			GL.End();
			//draw walls
			Vector3 blf = new Vector3(0,-wall_height,0);
			Vector3 brf = new Vector3(width * scale,-wall_height,0);
			Vector3 brb = new Vector3(width * scale,-wall_height, -width * scale);
			Vector3 blb = new Vector3(0,-wall_height,-width * scale);

			//draw front wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 0.3f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.7f  , 0.2f  , 1f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(0.0f   , 0.15f  , 0.0f  , 1f));
				GL.Normal3(0,0,1);
				for (int i = width; i >= 0; i--) {
					GL.Vertex3(blf + (brf - blf)*i/(float)width);
 					GL.Vertex3(i*scale, terrain_vertices[i,0], 0);
				}
			}
			GL.End();

			//draw right wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 0.3f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.7f  , 0.2f  , 1f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(0.0f   , 0.15f  , 0.0f  , 1f));
				GL.Normal3(1,0,0);			
				for (int i = width; i >= 0; i--) {					
					GL.Vertex3(brf + (brb - brf) * i/(float)width);
					GL.Vertex3(width * scale, terrain_vertices[width, i], - i * scale);
				}
			}
			GL.End();
			
			//draw left wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 0.3f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.7f  , 0.2f  , 1f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(0.0f   , 0.15f  , 0.0f  , 1f));
				GL.Normal3(-1,0,0);		
				for (int i = 0; i <= width; i++) {
					GL.Vertex3(blf + (blb - blf) * i/(float)width);
					GL.Vertex3(0, terrain_vertices[0, i], -i * scale);
				}
			}
			GL.End();
			
			//draw back wall
			GL.Begin(PrimitiveType.QuadStrip);
			{				
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 0.3f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.7f  , 0.2f  , 1f));
				GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(0.0f   , 0.15f  , 0.0f  , 1f));
				GL.Normal3(0,0,-1);

				for (int i = 0; i <= width; i++) {
					GL.Vertex3(blb + (brb - blb) * i/(float)width);
					GL.Vertex3(i*scale, terrain_vertices[i, width], -width * scale);
				}
			}
			GL.End();

			//draw bottom
			GL.Begin(PrimitiveType.Quads);
			{
				GL.Normal3(0,-1,0);
				GL.Vertex3(brf);
				GL.Vertex3(blf);
				GL.Vertex3(blb);
				GL.Vertex3(brb);
			}
			GL.End();
		}
	}
}

