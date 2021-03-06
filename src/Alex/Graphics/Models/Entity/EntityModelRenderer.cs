﻿using System;
using System.Collections.Generic;
using System.Linq;
using Alex.API.Graphics;
using Alex.API.Utils;
using Alex.ResourcePackLib.Json.Models.Entities;
using Alex.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using NLog;
using MathF = System.MathF;

namespace Alex.Graphics.Models.Entity
{
	public partial class EntityModelRenderer : Model, IDisposable
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(EntityModelRenderer));

		//private EntityModel Model { get; }
		private IReadOnlyDictionary<string, ModelBone> Bones { get; }
		public PooledTexture2D Texture { get; set; }
		private PooledVertexBuffer VertexBuffer { get; set; }
		public bool Valid { get; private set; }
		private bool CanRender { get; set; } = true;

		public long Vertices => CanRender && VertexBuffer != null ? VertexBuffer.VertexCount : 0;
		//public float Height { get; private set; } = 0f;
		public EntityModelRenderer(EntityModel model, PooledTexture2D texture)
		{
			
		//	Model = model;
			Texture = texture;

			if (texture == null)
			{
				Log.Warn($"No texture set for rendererer for {model.Name}!");
				return;
			}

			if (model != null)
			{
				var cubes = new Dictionary<string, ModelBone>();
				Cache(model, cubes);

				Bones = cubes;

				Valid = true;
			}
		}

		private void Cache(EntityModel model, Dictionary<string, ModelBone> modelBones)
		{
			List<VertexPositionNormalTexture> vertices = new List<VertexPositionNormalTexture>();

			var textureSize = new Vector2(model.Texturewidth, model.Textureheight);
			var newSize = new Vector2(Texture.Width, Texture.Height);

			if (textureSize.X == 0 && textureSize.Y == 0)
				textureSize = newSize;
			
			var uvScale = newSize / textureSize;
			
			foreach (var bone in model.Bones.Where(x => string.IsNullOrWhiteSpace(x.Parent)))
			{
				//if (bone.NeverRender) continue;
				if (modelBones.ContainsKey(bone.Name)) continue;
				
				ProcessBone(bone, vertices, uvScale, textureSize, modelBones);
			}
			
			foreach (var bone in model.Bones.Where(x => !string.IsNullOrWhiteSpace(x.Parent)))
			{
				//if (bone.NeverRender) continue;
				if (modelBones.ContainsKey(bone.Name)) continue;

				var newBone = ProcessBone(bone, vertices, uvScale, textureSize, modelBones);

				if (modelBones.TryGetValue(bone.Parent, out ModelBone parentBone))
				{
					parentBone.Children = parentBone.Children.Concat(new[] {newBone}).ToArray();
				}
			}
			
			if (vertices.Count == 0)
			{
			//	Log.Warn($"No vertices. {JsonConvert.SerializeObject(model,Formatting.Indented)}");
				Valid = true;
				CanRender = false;
				return;
			}

			VertexBuffer = GpuResourceManager.GetBuffer(this, Alex.Instance.GraphicsDevice,
				VertexPositionNormalTexture.VertexDeclaration, vertices.Count, BufferUsage.None);
			VertexBuffer.SetData(vertices.ToArray());
			
			Valid = true;
		}

		private ModelBone ProcessBone(EntityModelBone bone, List<VertexPositionNormalTexture> vertices, Vector2 uvScale, Vector2 textureSize, Dictionary<string, ModelBone> modelBones)
		{
			List<ModelBoneCube> cubes = new List<ModelBoneCube>();
			ModelBone           modelBone;
				
			if (bone.Cubes != null)
			{
				foreach (var cube in bone.Cubes)
				{
					if (cube == null)
					{
						Log.Warn("Cube was null!");
						continue;
					}

					var size     = cube.Size;
					var origin   = cube.Origin;
					var pivot    = cube.Pivot;
					var rotation = cube.Rotation;

					origin = new Vector3(-(origin.X + size.X), origin.Y, origin.Z);
					
					//VertexPositionNormalTexture[] vertices;
					Cube built = new Cube(size * (float)cube.Inflate, textureSize);
					built.Mirrored = bone.Mirror;
					built.BuildCube(cube.Uv * uvScale);
					vertices = ModifyCubeIndexes(vertices, ref built.Front);
					vertices = ModifyCubeIndexes(vertices, ref built.Back);
					vertices = ModifyCubeIndexes(vertices, ref built.Top);
					vertices = ModifyCubeIndexes(vertices, ref built.Bottom);
					vertices = ModifyCubeIndexes(vertices, ref built.Left);
					vertices = ModifyCubeIndexes(vertices, ref built.Right);

					var part = new ModelBoneCube(built.Front.indexes
					   .Concat(built.Back.indexes)
					   .Concat(built.Top.indexes)
					   .Concat(built.Bottom.indexes)
					   .Concat(built.Left.indexes)
					   .Concat(built.Right.indexes)
					   .ToArray(), Texture, rotation, pivot, origin);

					part.Mirror = bone.Mirror;
					cubes.Add(part);
				}
			}

			modelBone = new ModelBone(cubes.ToArray(), bone.Parent, bone);

			modelBone.UpdateRotationMatrix = !bone.NeverRender;
			if (!modelBones.TryAdd(bone.Name, modelBone))
			{
				Log.Debug($"Failed to add bone! {bone.Name}");
			}

			return modelBone;
		}

		private List<VertexPositionNormalTexture> ModifyCubeIndexes(List<VertexPositionNormalTexture> vertices,
			ref (VertexPositionNormalTexture[] vertices, short[] indexes) data)
		{
			var startIndex = (short)vertices.Count;
			foreach (var vertice in data.vertices)
			{
				var vertex = vertice;
				vertices.Add(vertex);
			}

			for (int i = 0; i < data.indexes.Length; i++)
			{
				data.indexes[i] += startIndex;
			}

			return vertices;
		}

		private static RasterizerState RasterizerState = new RasterizerState()
		{
			DepthBias = 0.0001f,
			CullMode = CullMode.CullClockwiseFace,
			FillMode = FillMode.Solid
		};
		
		public virtual void Render(IRenderArgs args, PlayerLocation position, bool mock)
		{
			if (!CanRender)
				return;
			
			var originalRaster = args.GraphicsDevice.RasterizerState;
			var blendState = args.GraphicsDevice.BlendState;

			try
			{
				args.GraphicsDevice.BlendState = BlendState.AlphaBlend;
				args.GraphicsDevice.RasterizerState = RasterizerState;
				
				args.GraphicsDevice.SetVertexBuffer(VertexBuffer);

				if (Bones == null) return;

				foreach (var bone in Bones)
				{
					bone.Value.Render(args, position, CharacterMatrix, mock);
				}
			}
			finally
			{
				args.GraphicsDevice.RasterizerState = originalRaster;
				args.GraphicsDevice.BlendState = blendState;
			}
		}

		public Vector3 EntityColor { get; set; } = Color.White.ToVector3();
		public Vector3 DiffuseColor { get; set; } = Color.White.ToVector3();
		private Matrix CharacterMatrix { get; set; } = Matrix.Identity;
		public float Scale { get; set; } = 1f;

		public virtual void Update(IUpdateArgs args, PlayerLocation position)
		{
			if (Bones == null) return;

			CharacterMatrix = Matrix.CreateScale(Scale / 16f) *
			                         Matrix.CreateRotationY(MathUtils.ToRadians(180f - (position.Yaw))) *
			                         Matrix.CreateTranslation(position);

			foreach (var bone in Bones)
			{
				bone.Value.Update(args, CharacterMatrix, EntityColor * DiffuseColor);
			}

			foreach (var bone in Bones.Where(x => !string.IsNullOrWhiteSpace(x.Value.Parent)))
			{
				var parent = Bones.FirstOrDefault(x =>
					x.Key.Equals(bone.Value.Parent, StringComparison.InvariantCultureIgnoreCase));

				if (parent.Value != null)
				{
					bone.Value.RotationMatrix = parent.Value.RotationMatrix;
				}
			}
		}

		public bool GetBone(string name, out ModelBone bone)
		{
			if (string.IsNullOrWhiteSpace(name) || Bones == null || Bones.Count == 0)
			{
				bone = null;
				return false;
			}

			return Bones.TryGetValue(name, out bone);
		}

		public void Dispose()
		{
			if (Bones != null && Bones.Any())
			{
				foreach (var bone in Bones.ToArray())
				{
					bone.Value.Dispose();
				}
			}
			
			Texture?.MarkForDisposal();
			VertexBuffer?.MarkForDisposal();
		}
	}
}
