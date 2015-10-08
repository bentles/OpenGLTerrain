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
		public enum Triangle {tl, tr, bl, br}

		public class TerrainBlock
		{
			enum Diagonal {tlbr, trbl} ;
			public TerrainTriangle top;
			public TerrainTriangle bottom;
			Vector3 bl_helper;
			Vector3 br_helper;
			Vector3 tl_helper;
			Vector3 tr_helper;
			Diagonal type;
			int I;
			int J;

			public TerrainBlock(float porosity, float tl, float tr, float bl, float br, int i, int j, float scale) {
				type = (Math.Abs(tl - tr) < Math.Abs(tr - bl)) ? Diagonal.tlbr : Diagonal.trbl ;

				this.I = i;
				this.J = j;

				if (type == Diagonal.tlbr) {
					top = new TerrainTriangle(tr, tl, br, Triangle.tr, I, J, scale);
					bottom = new TerrainTriangle(tl, bl, br, Triangle.bl, I, J, scale);

					bl_helper = bottom.normal;
					br_helper = bottom.normal + top.normal;
					tl_helper = br_helper;
					tr_helper = top.normal;
				}
				else {
					top = new TerrainTriangle(tr, tl, bl, Triangle.tl , I, J, scale);
					bottom = new TerrainTriangle(tr, bl, br, Triangle.br ,I, J, scale);

					br_helper = bottom.normal;
					bl_helper = bottom.normal + top.normal;
					tr_helper = bl_helper;
					tl_helper = top.normal;
				}
			}

            public TerrainTriangle Left()
            {
                if (type == Diagonal.tlbr)
                    return bottom;
                else
                    return top;
            }

            public TerrainTriangle Right()
            {
                if (type == Diagonal.tlbr)
                    return top;
                else
                    return bottom;
            }

            public void Flow(int turn)
            {
                top.Flow(turn);
                bottom.Flow(turn);
            } 

            public void DiscoverNeighbours(TerrainBlock[,] blocks)
            {
                bool not_above = J - 1 < 0;
				bool not_below = J + 1 >= blocks.GetLength(0);
				bool not_left = I - 1 < 0;
				bool not_right = I + 1 >= blocks.GetLength(1);

                if (type == Diagonal.tlbr)
                {
					top.neighbours[0] = not_above ? null : blocks[I, J - 1].bottom;
					top.neighbours[1] = bottom;
                    top.neighbours[2] = not_right ? null : blocks[I + 1, J].Left();

					bottom.neighbours[0] = not_left ? null : blocks[I - 1, J].Right();
                    bottom.neighbours[1] = not_below? null : blocks[I, J + 1].top;
					bottom.neighbours[2] = top;
                }
                else
                {
					top.neighbours[0] = not_above ? null : blocks[I, J - 1].bottom;
                    top.neighbours[1] = not_left ? null : blocks[I - 1, J].Right();
					top.neighbours[2] = bottom;
                    
					bottom.neighbours[0] = top;
                    bottom.neighbours[1] = not_below ? null : blocks[I, J + 1].top ;
					bottom.neighbours[2] = not_right ? null : blocks[I + 1, J].Left();
                }
            }

			public void CalcNormals(TerrainBlock[,] blocks)
			{
				bool not_above = J - 1 < 0;
				bool not_below = J + 1 >= blocks.GetLength(0);
				bool not_left = I - 1 < 0;
				bool not_right = I + 1 >= blocks.GetLength(1);

				Vector3 top_r = not_above ? Vector3.Zero : blocks[I, J - 1].br_helper ;
				Vector3 top_l = not_above ? Vector3.Zero : blocks[I, J - 1].bl_helper ;
				Vector3 right_t = not_right ? Vector3.Zero : blocks[I + 1, J].tl_helper ;
				Vector3 right_b = not_right ? Vector3.Zero : blocks[I + 1, J].bl_helper ;

				Vector3 bot_r = not_below ? Vector3.Zero : blocks[I, J + 1].tr_helper ;
				Vector3 bot_l = not_below ? Vector3.Zero : blocks[I, J + 1].tl_helper ;
				Vector3 left_t = not_left ? Vector3.Zero : blocks[I - 1, J].tr_helper ;
				Vector3 left_b = not_left ? Vector3.Zero : blocks[I - 1, J].br_helper ;

				Vector3 tl = not_above || not_left ? Vector3.Zero : blocks[I -1, J-1].br_helper ;
				Vector3 tr = not_above || not_right ? Vector3.Zero : blocks[I +1, J-1].bl_helper ;
				Vector3 bl = not_below || not_left ? Vector3.Zero : blocks[I -1, J+1].tr_helper ;
				Vector3 br = not_below || not_right ? Vector3.Zero : blocks[I + 1, J+1].tl_helper ;

				if (type == Diagonal.tlbr) {
					//top triangle
					Vector3 ttr = top.normal + top_r + tr + right_t ;
					Vector3 ttl = top.normal + bottom.normal + top_l + tl + left_t ;
					Vector3 tbr = top.normal + bottom.normal + bot_r + br + right_b ;

					top.CalcNormals(ttr, ttl, tbr);

					//bottom triangle
					Vector3 btl = ttl;
					Vector3 bbl = bottom.normal + bot_l + bl + left_b ;
					Vector3 bbr = tbr;

					bottom.CalcNormals(btl, bbl, bbr);
				}
				else {
					//bottom triangle
					Vector3 btr = top.normal + bottom.normal + top_r + tr + right_t ;
					Vector3 bbl = top.normal + bottom.normal + bot_l + bl + left_b ;
					Vector3 bbr = bottom.normal + bot_r + br + right_b ;

					bottom.CalcNormals(btr, bbl, bbr);

					//top triangle
					Vector3 ttr = btr;
					Vector3 ttl = top.normal + top_l + tl + left_t ;
					Vector3 tbl = bbl;

					top.CalcNormals(ttr,ttl, tbl);
				}
			}


			public void Draw(bool flat, bool downhills)
			{
				top.Draw(flat, downhills);
				bottom.Draw(flat, downhills);
			}
		}

		public class TerrainTriangle
		{
            //variables for dealing with water flow
			public float low, high, porosity, flow;
            public float ground_capacity;
            public float surface_water, ground_water;
            public float new_surface_water, new_ground_water;

            int turn = 0;           

            public Vector3[] points;
            public Vector3[] normals;
			public Vector3 normal;
            public Vector3 downhill;

            //triangle_side_01, triangle_side_12, triangle_side_20;
            public TerrainTriangle[] neighbours = new TerrainTriangle[3];
            //normals for each direction
            //side_01, side_12, side_20;
            public Vector3[] neighbour_normals = new Vector3[3];

            //where the triangle is
            public Triangle t;

            //start at the highest, rightmost point. work anticlockwise. highest has priority
			public TerrainTriangle(float a, float b, float c, Triangle t, int I, int J, float scale)
			{
                this.t = t;
                surface_water = 0.3f;
                new_surface_water = surface_water;
                ground_water = 0;
                new_ground_water = ground_water;
           
				points = new Vector3[3];
				normals = new Vector3[3];

				float tlX = I * scale;
				float tlY = J * scale;

				if (t == Triangle.bl) {
					points[0] = new Vector3(tlX  , a, tlY);
					points[1] = new Vector3(tlX, b, tlY + scale);
					points[2] = new Vector3(tlX + scale, c, tlY + scale);

				}
				else if (t == Triangle.br) {
					points[0] = new Vector3(tlX + scale, a, tlY);
					points[1] = new Vector3(tlX, b, tlY + scale);
					points[2] = new Vector3(tlX + scale, c, tlY + scale);

				}
				else if (t == Triangle.tr) {
					points[0] = new Vector3(tlX + scale, a, tlY);
					points[1] = new Vector3(tlX, b, tlY);
					points[2] = new Vector3(tlX + scale, c, tlY + scale);
											
				}						   
				else {					  
					points[0] = new Vector3(tlX + scale, a, tlY);
					points[1] = new Vector3(tlX, b, tlY);
					points[2] = new Vector3(tlX, c, tlY + scale);
				}

				//find the low point of the triangle
				low = points[0].Y;
				low = (low > points[1].Y) ? points[1].Y : low;
				low = (low > points[2].Y) ? points[2].Y : low;

                //find the high point of the triangle
                high = points[0].Y;
                high = (high < points[1].Y) ? points[1].Y : high;
                high = (high < points[2].Y) ? points[2].Y : high;

				normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).Normalized();
				downhill = Vector3.Cross(Vector3.Cross(normal, -Vector3.UnitY), normal).Normalized();

				if (Vector3.Dot(downhill, -Vector3.UnitY) < 0)
					downhill = -downhill;

                float slope = -downhill.Y / downhill.X;
                float inv_slope = 1 / (slope);

                //find side normals                
                neighbour_normals[0] = Vector3.Cross(points[1] - points[0], normal).Normalized();
                neighbour_normals[1] = Vector3.Cross(points[2] - points[1], normal).Normalized();
                neighbour_normals[2] = Vector3.Cross(points[0] - points[2], normal).Normalized();

                //triangles to each side
                neighbours[0] = null;
                neighbours[1] = null;
                neighbours[2] = null;

                //modified sigmoid fn to constrain porosity to sane amounts for small slopes
                porosity = 1 / (1 + (float)Math.Exp(-inv_slope + 6));
                ground_capacity = porosity;
                flow = 1 / (1 + (float)Math.Exp(-slope + 6));
			}

            public void TryUpdate(int cur_turn)
            {
                if (turn != cur_turn)
                {
                    turn = cur_turn;
                    surface_water = new_surface_water;
                    ground_water = new_ground_water;
                }                
            }

            public bool IsInert()
            {
                return surface_water <= 0;
            }

            public float Height()
            {
                return low + surface_water;
            }

            public void Flow(int cur_turn)
            {
                TryUpdate(cur_turn);

                if (!IsInert())
                {
                    float seep_speed = 0.01f;

                    //send some water into the ground
                    float seep = (ground_capacity - ground_water) > porosity * seep_speed ? porosity * seep_speed : (ground_capacity - ground_water);
                    new_surface_water -= seep;
                    new_ground_water += seep;

                    float total = 0;
                    foreach (var neigh in neighbour_normals)
	                {
                        float a = Vector3.Dot(downhill, neigh);
                        total = a < 0 ? total : total + a;
	                }


                    //send some water into neighbours
                    for (int i = 0; i < neighbours.Length; i++)
                    {   
                        if (neighbours[i] != null)
                        {
                            float a = Vector3.Dot(downhill, neighbour_normals[i]);
                            a = a < 0 ? 0 : a;
                            float direction_modifier = a / total;

                            //water above high mark
                            float highwater = low + surface_water - high;

                            float diff = Height() - neighbours[i].Height();
                            if (diff > highwater / 3f)
                                diff = highwater / 3f;
                            if (diff < -highwater / 3f)
                                diff = -highwater / 3f;

                            //water below high mark
                            float flow_speed = 0.65f;
                            //multiply by direction modifier
                            float lowdiff = flow_speed * direction_modifier;

                            //actual updates
                            neighbours[i].TryUpdate(cur_turn);

							//water below high mark
							if (high > neighbours[i].Height())
							{
								neighbours[i].new_surface_water += lowdiff;
								new_surface_water -= lowdiff;
							}  
                  
                            neighbours[i].new_surface_water += diff * flow_speed / 3f;
                            new_surface_water -= diff * flow_speed / 3f;
                            
                                                   
                        }
                    }
                }                
            }

			//start at the highest, rightmost point. work anticlockwise. highest has priority
			public void CalcNormals(Vector3 a, Vector3 b, Vector3 c)
			{
				normals[0] = a.Normalized();
				normals[1] = b.Normalized();
				normals[2] = c.Normalized();
			}

			public void Draw(bool flat, bool downhills)
			{
				//ground
				GL.Begin(PrimitiveType.Triangles);
				{
					float greendex = ground_water / ground_capacity;

					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4((greendex*-0.8f) + 0.8f   , (greendex* - 0.35f) + 0.8f  , (greendex * -0.5f) + 0.5f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(0.3f   , 0.3f  , 0.3f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 10);

					GL.Normal3(normal);
					for (int i = 0; i < points.Length; i++) {
						if (!flat)
							GL.Normal3(normals[i]);
						GL.Vertex3(points[i]);
					}
				}
				GL.End();

				if (surface_water > 0.01f)
				{
					//water
					GL.Begin(PrimitiveType.Triangles);
					{
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.1f, 0.1f, 0.1f, 1f));
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.2f   , 0.2f  , 0.8f  , 1f));
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(1f   , 1f  , 1f  , 1f));
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 1024);
						for (int i = 0; i < points.Length; i++) {
							if (!flat)
								GL.Normal3(normals[i]);
							if (Height() < high)
	                        	GL.Vertex3(points[i] + new Vector3(0, surface_water, 0));
							else {
								GL.Normal3(Vector3.UnitY);
								GL.Vertex3(new Vector3(points[i].X, low + surface_water, points[i].Z));
							}
						}
					}
					GL.End();
				}
                
				//downhill vectors
				if (downhills)
				{
					Vector3 st = Vector3.Lerp(Vector3.Lerp(points[0], points[1], 0.5f), points[2], 0.3333f);
					GL.Begin(PrimitiveType.Lines);
					{
						GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.3f, 0.3f, 1f, 0.3f));
						GL.Vertex3(st);
						GL.Vertex3(st + downhill);
					}
					GL.End();
					
					GL.PointSize(3f);
					GL.Begin(PrimitiveType.Points);
					{
						GL.Vertex3(st + downhill);
					}
					GL.End();	
				}
			}
		}
			
		public bool draw_downhill = false;
		public bool flat = false;

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

			//create terrain
			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {					
					foreach (var item in noise_gen) {
						if (item.add)
							terrain_vertices[i,j] += item.getNoise(i,j);
						else
							terrain_vertices[i,j] *= item.getNoise(i,j);
					}	

					terrain_vertices[i,j] *= scale;

					if (i > 0 && j > 0) {
						//make and store info about terrain from vertices
						terrain_blocks[i-1,j-1] = new TerrainBlock(5, 
							terrain_vertices[i-1, j-1], terrain_vertices[i, j-1], 
							terrain_vertices[i-1, j], terrain_vertices[i, j], 
							i-1, j-1, scale);
					}
				}
			}

			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {
					if (i > 0 && j > 0) {
                        //compute normals for gouraud shading
						terrain_blocks[i - 1, j - 1].CalcNormals(terrain_blocks);
                        //discover neighbours for all terrain triangles
                        terrain_blocks[i - 1, j - 1].DiscoverNeighbours(terrain_blocks);
					}
				}
			}

		}

		public void Draw(bool flat, bool downhills) {
			int width = terrain_vertices.GetLength(0) - 1;
			int height = terrain_vertices.GetLength(1) - 1;

			float wall_height = 30 * scale;


			//draw terrain
			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {
					terrain_blocks[i,j].Draw(flat, downhills);
				}
			}
				
			//draw walls
			Vector3 blb = new Vector3(0,-wall_height,0);
			Vector3 brb = new Vector3(width * scale,-wall_height,0);
			Vector3 brf = new Vector3(width * scale,-wall_height, width * scale);
			Vector3 blf = new Vector3(0,-wall_height, width * scale);		

			//draw front wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				
				for (int i = width; i >= 0; i--) {
					GL.Normal3(new Vector3(0,0,1));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, new Color4(0.15f, 0.15f, 0.15f, 0.3f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse,  new Color4(0.18f   , 0.18f  , 0.18f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular,  new Color4(1f   , 1f  , 1f  , 1f));
					GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess,  100f);

					GL.Vertex3(blf + (brf - blf)*i/(float)width);
					GL.Vertex3(i*scale, terrain_vertices[i,width], width*scale);
				}
			}
			GL.End();

			//draw right wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				for (int i = 0; i <=  width; i++) {					
					GL.Normal3(new Vector3(1,0,0));			
					GL.Vertex3(brb + (brf - brb) * i/(float)width);
					GL.Vertex3(width * scale, terrain_vertices[width, i], i * scale);
				}
			}
			GL.End();
			
			//draw left wall
			GL.Begin(PrimitiveType.QuadStrip);
			{
				for (int i = width; i >= 0; i--) {
					GL.Normal3(new Vector3(-1,0,0));
					GL.Vertex3(blb + (blf - blb) * i/(float)width);
					GL.Vertex3(0, terrain_vertices[0, i], i * scale);
				}
			}
			GL.End();
			
			//draw back wall
			GL.Begin(PrimitiveType.QuadStrip);
			{				

				for (int i = 0; i <= width; i++) {
					GL.Normal3(new Vector3(0,0,-1));
					GL.Vertex3(blb + (brb - blb) * i/(float)width);
					GL.Vertex3(i*scale, terrain_vertices[i, 0], 0);
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

        public void UpdateState(int tick)
        {
            int width = terrain_vertices.GetLength(0) - 1;
            int height = terrain_vertices.GetLength(1) - 1;

            //draw terrain
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    //make water flow
                    terrain_blocks[i, j].Flow(tick);
                }
            }

        }
	}
}

