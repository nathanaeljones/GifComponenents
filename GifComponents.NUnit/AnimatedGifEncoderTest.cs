#region Copyright (C) Simon Bridewell
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 3
// of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

// You can read the full text of the GNU General Public License at:
// http://www.gnu.org/licenses/gpl.html

// See also the Wikipedia entry on the GNU GPL at:
// http://en.wikipedia.org/wiki/GNU_General_Public_License
#endregion

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using NUnit.Extensions;
using CommonForms.Responsiveness;
using GifComponents.Components;
using GifComponents.NUnit.Components;
using GifComponents.NUnit.Tools;
using GifComponents.Palettes;
using GifComponents.Tools;

namespace GifComponents.NUnit
{
	/// <summary>
	/// Test fixture for the AnimatedGifEncoder class.
	/// </summary>
	[TestFixture]
	public class AnimatedGifEncoderTest : GifComponentTestFixtureBase, IDisposable
	{
		private AnimatedGifEncoder _e;
		private GifDecoder _d;
		private Collection<GifFrame> _frames;
		
		#region WikipediaExampleTest
		/// <summary>
		/// Uses the encoder to create a GIF file using the example at
		/// http://en.wikipedia.org/wiki/Gif#Example_.gif_file and then reads
		/// back in and verifies all its components.
		/// </summary>
		[Test]
		[SuppressMessage("Microsoft.Naming", 
		                 "CA1704:IdentifiersShouldBeSpelledCorrectly", 
		                 MessageId = "Wikipedia")]
		public void WikipediaExampleTest()
		{
			ReportStart();
			_e = new AnimatedGifEncoder();
			GifFrame frame = new GifFrame( WikipediaExample.ExpectedBitmap );
			frame.Delay = WikipediaExample.DelayTime;
			_e.AddFrame( frame );
			
			// TODO: some way of creating/testing a UseLocal version of WikipediaExample
			string fileName = "WikipediaExampleUseGlobal.gif";
			_e.WriteToFile( fileName );
			Stream s = File.OpenRead( fileName );
			
			int code;

			// check GIF header
			GifHeader gh = new GifHeader( s );
			Assert.AreEqual( ErrorState.Ok, gh.ConsolidatedState );

			// check logical screen descriptor
			LogicalScreenDescriptor lsd = new LogicalScreenDescriptor( s );
			Assert.AreEqual( ErrorState.Ok, lsd.ConsolidatedState );
			WikipediaExample.CheckLogicalScreenDescriptor( lsd );
			
			// read global colour table
			ColourTable gct 
				= new ColourTable( s, WikipediaExample.GlobalColourTableSize );
			Assert.AreEqual( ErrorState.Ok, gct.ConsolidatedState );
			// cannot compare global colour table as different encoders will
			// produce difference colour tables.
//			WikipediaExample.CheckGlobalColourTable( gct );
			
			// check for extension introducer
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeExtensionIntroducer, code );
			
			// check for app extension label
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeApplicationExtensionLabel, code );
			
			// check netscape extension
			ApplicationExtension ae = new ApplicationExtension( s );
			Assert.AreEqual( ErrorState.Ok, ae.ConsolidatedState );
			NetscapeExtension ne = new NetscapeExtension( ae );
			Assert.AreEqual( ErrorState.Ok, ne.ConsolidatedState );
			Assert.AreEqual( 0, ne.LoopCount );
			
			// check for extension introducer
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeExtensionIntroducer, code );

			// check for gce label
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeGraphicControlLabel, code );
			
			// check graphic control extension
			GraphicControlExtension gce = new GraphicControlExtension( s );
			Assert.AreEqual( ErrorState.Ok, gce.ConsolidatedState );
			WikipediaExample.CheckGraphicControlExtension( gce );

			// check for image separator
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeImageSeparator, code );
			
			// check for image descriptor
			ImageDescriptor id = new ImageDescriptor( s );
			Assert.AreEqual( ErrorState.Ok, id.ConsolidatedState );
			WikipediaExample.CheckImageDescriptor( id );

			// read, decode and check image data
			// Cannot compare encoded LZW data directly as different encoders
			// will create different colour tables, so even if the bitmaps are
			// identical, the colour indices will be different
			int pixelCount = WikipediaExample.FrameSize.Width
							* WikipediaExample.FrameSize.Height;
			TableBasedImageData tbid = new TableBasedImageData( s, pixelCount );
			for( int y = 0; y < WikipediaExample.LogicalScreenSize.Height; y++ )
			{
				for( int x = 0; x < WikipediaExample.LogicalScreenSize.Width; x++ )
				{
					int i = (y * WikipediaExample.LogicalScreenSize.Width) + x;
					Assert.AreEqual( WikipediaExample.ExpectedBitmap.GetPixel( x, y ),
					                 gct[tbid.Pixels[i]],
					                 "X: " + x + ", Y: " + y );
				}
			}

			// Check for block terminator after image data
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( 0x00, code );
			
			// check for GIF trailer
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeTrailer, code );
			
			// check we're at the end of the stream
			code = ExampleComponent.CallRead( s );
			Assert.AreEqual( -1, code );
			s.Close();
			
			_d = new GifDecoder( fileName );
			_d.Decode();
			Assert.AreEqual( ErrorState.Ok, _d.ConsolidatedState );
			BitmapAssert.AreEqual( WikipediaExample.ExpectedBitmap, 
			                      (Bitmap) _d.Frames[0].TheImage,
			                       "" );
			ReportEnd();
		}
		#endregion

		#region UseGlobal
		/// <summary>
		/// Tests the encoder using a two-frame 2x2 pixel checkerboard animation
		/// with a global colour table.
		/// </summary>
		[Test]
		public void UseGlobal()
		{
			ReportStart();
			_frames = new Collection<GifFrame>();
			_frames.Add( new GifFrame( (Image) Bitmap1() ) );
			_frames.Add( new GifFrame( (Image) Bitmap2() ) );
			TestAnimatedGifEncoder( ColourTableStrategy.UseGlobal, 
			                        8, 
			                        Bitmap1().Size );
			ReportEnd();
		}
		#endregion

		#region UseLocal
		/// <summary>
		/// Tests the encoder using a two-frame 2x2 pixel checkerboard animation
		/// with local colour tables.
		/// </summary>
		[Test]
		public void UseLocal()
		{
			ReportStart();
			_frames = new Collection<GifFrame>();
			_frames.Add( new GifFrame( (Image) Bitmap1() ) );
			_frames.Add( new GifFrame( (Image) Bitmap2() ) );
			TestAnimatedGifEncoder( ColourTableStrategy.UseLocal, 
			                        11, 
			                        Bitmap1().Size );
			ReportEnd();
		}
		#endregion

		#region DecodeEncodeGlobeGlobal
		/// <summary>
		/// Decodes the rotating globe animation, creates a new animation using
		/// its frames and a global colour table, and checks that the encoded 
		/// images are the same as the original images.
		/// </summary>
		[Test]
		public void DecodeEncodeGlobeGlobal()
		{
			ReportStart();
			string filename = @"images/globe/spinning globe better 200px transparent background.gif";
			_d = new GifDecoder( filename );
			_d.Decode();
			_e = new AnimatedGifEncoder();
			_e.ColourTableStrategy = ColourTableStrategy.UseGlobal;
			foreach( GifFrame f in _d.Frames )
			{
				_e.AddFrame( new GifFrame( f.TheImage ) );
			}
			MemoryStream s = new MemoryStream();
			_e.WriteToStream( s );
			s.Seek( 0, SeekOrigin.Begin );
			_d = new GifDecoder( s );
			_d.Decode();
			CheckFrames();
			ReportEnd();
		}
		#endregion
		
		#region DecodeEncodeGlobeLocal
		/// <summary>
		/// Decodes the rotating globe animation, creates a new animation using
		/// its frames and local colour tables, and checks that the encoded 
		/// images are the same as the original images.
		/// </summary>
		[Test]
		public void DecodeEncodeGlobeLocal()
		{
			ReportStart();
			string filename = @"images/globe/spinning globe better 200px transparent background.gif";
			_d = new GifDecoder( filename );
			_d.Decode();
			_e = new AnimatedGifEncoder();
			_e.ColourTableStrategy = ColourTableStrategy.UseLocal;
			foreach( GifFrame f in _d.Frames )
			{
				_e.AddFrame( new GifFrame( f.TheImage ) );
			}
			MemoryStream s = new MemoryStream();
			_e.WriteToStream( s );
			s.Seek( 0, SeekOrigin.Begin );
			_d = new GifDecoder( s );
			_d.Decode();
			CheckFrames();
			ReportEnd();
		}
		#endregion
		
		#region DecodeEncodeSmileyGlobal
		/// <summary>
		/// Decodes the smiley animation, creates a new animation using its 
		/// frames and a global colour table, and checks that the encoded 
		/// images are the same as the original images.
		/// </summary>
		[Test]
		public void DecodeEncodeSmileyGlobal()
		{
			ReportStart();
			string filename = @"images/smiley/smiley.gif";
			_d = new GifDecoder( filename );
			_d.Decode();
			_e = new AnimatedGifEncoder();
			_e.ColourTableStrategy = ColourTableStrategy.UseGlobal;
			foreach( GifFrame f in _d.Frames )
			{
				_e.AddFrame( new GifFrame( f.TheImage ) );
			}
			MemoryStream s = new MemoryStream();
			_e.WriteToStream( s );
			s.Seek( 0, SeekOrigin.Begin );
			_d = new GifDecoder( s );
			_d.Decode();
			CheckFrames();
			ReportEnd();
		}
		#endregion
		
		#region DecodeEncodeSmileyLocal
		/// <summary>
		/// Decodes the smiley animation, creates a new animation using its 
		/// frames and local colour tables, and checks that the encoded 
		/// images are the same as the original images.
		/// </summary>
		[Test]
		public void DecodeEncodeSmileyLocal()
		{
			ReportStart();
			string filename = @"images/smiley/smiley.gif";
			_d = new GifDecoder( filename );
			_d.Decode();
			_e = new AnimatedGifEncoder();
			_e.ColourTableStrategy = ColourTableStrategy.UseLocal;
			foreach( GifFrame f in _d.Frames )
			{
				_e.AddFrame( new GifFrame( f.TheImage ) );
			}
			MemoryStream s = new MemoryStream();
			_e.WriteToStream( s );
			s.Seek( 0, SeekOrigin.Begin );
			_d = new GifDecoder( s );
			_d.Decode();
			CheckFrames();
			ReportEnd();
		}
		#endregion
		
		#region private CheckFrames method
		private void CheckFrames()
		{
			Assert.AreEqual( ErrorState.Ok, _d.ConsolidatedState );
			Assert.AreEqual( _e.Frames.Count, _d.Frames.Count );
			for( int i = 0; i < _e.Frames.Count; i++ )
			{
				ImageAssert.AreEqual( _e.Frames[i].TheImage, 
				                      _d.Frames[i].TheImage,
				                      "Frame " + i );
			}
		}
		#endregion
		
		#region ColourQualityTooLow
		/// <summary>
		/// Checks that the colour quantization quality is set to the correct
		/// value when given a value which is too low.
		/// </summary>
		[Test]
		public void ColourQualityTooLow()
		{
			ReportStart();
			_e = new AnimatedGifEncoder();
			_e.SamplingFactor = 0;
			Assert.AreEqual( 1, _e.SamplingFactor );
			ReportEnd();
		}
		#endregion
		
		#region WriteToStreamNoFramesTest
		/// <summary>
		/// Checks that the correct exception is thrown when the WriteToStream
		/// method is called when no frames have been added to the 
		/// AnimatedGifEncoder.
		/// </summary>
		[Test]
		[ExpectedException( typeof( InvalidOperationException ) )]
		public void WriteToStreamNoFramesTest()
		{
			ReportStart();
			_e = new AnimatedGifEncoder();
			MemoryStream s = new MemoryStream();
			try
			{
				_e.WriteToStream( s );
			}
			catch( InvalidOperationException ex )
			{
				string message
					= "The AnimatedGifEncoder has no frames to write!";
				StringAssert.Contains( message, ex.Message );
				ReportEnd();
				throw;
			}
		}
		#endregion
		
		#region CompareQuantizers
		/// <summary>
		/// Compares the results of encoding an animation using the NeuQuant and
		/// Octree quantizers.
		/// </summary>
		[Test]
		[SuppressMessage("Microsoft.Naming", 
		                 "CA1704:IdentifiersShouldBeSpelledCorrectly", 
		                 MessageId = "Quantizers")]
		[Ignore( "Takes a while to run" )]
		public void CompareQuantizers()
		{
			ReportStart();
			
			string fileName;
			DateTime startTime;
			DateTime endTime;
			TimeSpan encodingTime;
			
			// Test actual quantization using image with 256+ colours.
			string bitmapFileName = "images/" + TestFixtureName 
			                       + "." + TestCaseName + ".bmp";

			Bitmap b = new Bitmap( bitmapFileName );
			
			for( int q = 1; q <= 20; q++ )
			{
				_e = new AnimatedGifEncoder();
				_e.QuantizerType = QuantizerType.NeuQuant;
				_e.SamplingFactor = q;
				_e.AddFrame( new GifFrame( b ) );
				fileName = TestFixtureName + "." + TestCaseName + ".NeuQuant." + q + ".gif";
				startTime = DateTime.Now;
				_e.WriteToFile( fileName );
				endTime = DateTime.Now;
				encodingTime = endTime - startTime;
				WriteMessage( "Encoding with quantization using NeuQuant, quality="
				              + q + " took " + encodingTime );
				_d = new GifDecoder( fileName );
				_d.Decode();
				Assert.AreEqual( ErrorState.Ok, _d.ConsolidatedState, "Quality " + q );
				Assert.AreEqual( 1, _d.Frames.Count, "Quality " + q );
				// FIXME: NeuQuant quantizer reducing to 180 colours instead of 256 colours
				// TODO: Check for exactly 256 colours once Octree quantizer returns 256-colour images
//				Assert.AreEqual( 256, ImageTools.GetDistinctColours( colours ).Count );
				Assert.LessOrEqual( ImageTools.GetDistinctColours( _d.Frames[0].TheImage ).Count, 256 );
				for( int tolerance = 0; tolerance < 256; tolerance++ )
				{
					try
					{
						ImageAssert.AreEqual( b, 
						                      _d.Frames[0].TheImage, 
						                      tolerance, 
						                      "Quality " + q );
						WriteMessage( "Quality " + q 
						             + " required tolerance " + tolerance );
						break;
					}
					catch( AssertionExtensionException )
					{
						if( tolerance == 255 )
						{
							throw;
						}
					}
				}
			}
			
			_e = new AnimatedGifEncoder();
			_e.QuantizerType = QuantizerType.Octree;
			_e.AddFrame( new GifFrame( b ) );
			fileName = TestFixtureName + "." + TestCaseName + ".Octree.gif";
			startTime = DateTime.Now;
			_e.WriteToFile( fileName );
			endTime = DateTime.Now;
			encodingTime = endTime - startTime;
			WriteMessage( "Encoding with quantization using Octree took " + encodingTime );
			_d = new GifDecoder( fileName );
			_d.Decode();
			Assert.AreEqual( ErrorState.Ok, _d.ConsolidatedState );
			Assert.AreEqual( 1, _d.Frames.Count );
			// FIXME: Octree quantizer should return a 256-colour image here
//			Assert.AreEqual( 256, ImageTools.GetDistinctColours( colours2 ).Count );
			Assert.LessOrEqual( ImageTools.GetDistinctColours( _d.Frames[0].TheImage ).Count, 256 );
			for( int tolerance = 0; tolerance < 256; tolerance++ )
			{
				try
				{
					ImageAssert.AreEqual( b, 
					                      _d.Frames[0].TheImage, 
					                      tolerance, 
					                      "Octree" );
					WriteMessage( "Octree quantization required tolerance " 
					             + tolerance );
					break;
				}
				catch( AssertionExtensionException )
				{
					if( tolerance == 255 )
					{
						throw;
					}
				}
			}
			
			// re-encoding an existing GIF should not cause quantization
			_d = new GifDecoder( @"images\globe\spinning globe better 200px transparent background.gif" );
			_d.Decode();
			
			_e = new AnimatedGifEncoder();
			// NB OctreeQuantizer does not support global colour tables (yet!)
			_e.ColourTableStrategy = ColourTableStrategy.UseLocal;
			foreach( GifFrame f in _d.Frames )
			{
				_e.AddFrame( new GifFrame( f.TheImage ) );
			}
			
			fileName = "NeuQuant.gif";
			_e.QuantizerType = QuantizerType.NeuQuant;
			startTime = DateTime.Now;
			_e.WriteToFile( fileName );
			endTime = DateTime.Now;
			encodingTime = endTime - startTime;
			WriteMessage( "Encoding without quantization using NeuQuant took " + encodingTime );
			
			fileName = "Octree.gif";
			_e.QuantizerType = QuantizerType.Octree;
			startTime = DateTime.Now;
			_e.WriteToFile( fileName );
			endTime = DateTime.Now;
			encodingTime = endTime - startTime;
			WriteMessage( "Encoding without quantization using Octree took " + encodingTime );
			
			GifDecoder nqDecoder = new GifDecoder( "NeuQuant.gif" );
			nqDecoder.Decode();
			GifDecoder otDecoder = new GifDecoder( "Octree.gif" );
			otDecoder.Decode();
			Assert.AreEqual( nqDecoder.Frames.Count, otDecoder.Frames.Count );
			for( int i = 0; i < nqDecoder.Frames.Count; i++ )
			{
				ImageAssert.AreEqual( nqDecoder.Frames[i].TheImage, 
				                      otDecoder.Frames[i].TheImage, 
				                      "frame " + i );
			}

			ReportEnd();
		}
		#endregion
		
		#region UseSuppliedPaletteGlobal
		/// <summary>
		/// Tests the encoder using a user-supplied palette and a global colour 
		/// table.
		/// </summary>
		[Test]
		public void UseSuppliedPaletteGlobal()
		{
			ReportStart();
			TestUseSuppliedPalette( ColourTableStrategy.UseGlobal );
			ReportEnd();
		}
		#endregion
		
		#region UseSuppliedPaletteLocal
		/// <summary>
		/// Tests the encoder using a user-supplied palette and local colour 
		/// tables.
		/// </summary>
		[Test]
		public void UseSuppliedPaletteLocal()
		{
			ReportStart();
			TestUseSuppliedPalette( ColourTableStrategy.UseLocal );
			ReportEnd();
		}
		#endregion
		
		#region NeuQuantTest
		/// <summary>
		/// Encodes a GIF file with a single frame consisting of a photo with
		/// more than 256 colours, and checks that the colours have been 
		/// quantized as expected.
		/// TODO: eventually belongs in NeuQuant's test fixture
		/// </summary>
		[Test]
		[SuppressMessage("Microsoft.Naming", 
		                 "CA1704:IdentifiersShouldBeSpelledCorrectly", 
		                 MessageId = "Neu")]
		public void NeuQuantTest()
		{
			ReportStart();
			_e = new AnimatedGifEncoder();
			_e.SamplingFactor = 10;
			_e.QuantizerType = QuantizerType.NeuQuant;
			_e.AddFrame( new GifFrame( Image.FromFile( @"images\MockOrange.jpg" ) ) );
			
			// TODO: add something to NUnit.Extensions to run LongRunningProcesses on a background thread
			Thread t = new Thread( DoEncode );
			t.IsBackground = true;
			t.Start();
			while( t.IsAlive )
			{
				foreach( ProgressCounter c in _e.AllProgressCounters.Values )
				{
					WriteMessage( c.ToString() );
				}
				Thread.Sleep( 1000 );
			}

			Image expected = Image.FromFile( @"images\MockOrange.gif" );
			Image actual = Image.FromFile( "MockOrange.test.gif" );
			ImageAssert.AreEqual( expected, actual );
			ReportEnd();
		}
		
		private void DoEncode()
		{
			_e.WriteToFile( "MockOrange.test.gif" );
		}
		#endregion
		
		#region private TestUseSuppliedPalette method
		private void TestUseSuppliedPalette( ColourTableStrategy strategy )
		{
			string globalLocal 
				= strategy == ColourTableStrategy.UseGlobal 
				? "Global"
				: "Local";
			// First, create and check a series of single-frame GIFs, one for
			// each of the available colour tables.
			string[] files = Directory.GetFiles( @"ColourTables", "*.act" );
			foreach( string act in files )
			{
				string actFileWithoutExtension 
					= Path.GetFileNameWithoutExtension( act );
				_e = new AnimatedGifEncoder();
				if( strategy == ColourTableStrategy.UseGlobal )
				{
					_e.Palette = Palette.FromFile( act );
					Assert.AreEqual( ColourTableStrategy.UseGlobal, 
					                 _e.ColourTableStrategy );
					// QuantizerType should default to UseSuppliedPalette when
					// the encoder's Palette property is set.
					Assert.AreEqual( QuantizerType.UseSuppliedPalette, 
					                 _e.QuantizerType );
				}
				else
				{
					_e.ColourTableStrategy = ColourTableStrategy.UseLocal;
					Assert.AreEqual( ColourTableStrategy.UseLocal, 
					                 _e.ColourTableStrategy );
					_e.QuantizerType = QuantizerType.UseSuppliedPalette;
					Assert.AreEqual( QuantizerType.UseSuppliedPalette, 
					                 _e.QuantizerType );
				}
				
				GifFrame frame 
					= new GifFrame( Image.FromFile( @"images\smiley.bmp" ) );
				if( strategy == ColourTableStrategy.UseLocal )
				{
					frame.Palette = Palette.FromFile( act );
				}
				_e.AddFrame( frame );
				
				string fileName 
					= "AnimatedGifEncoderTest.UseSuppliedPalette"
					+ globalLocal
					+ "-"
					+ actFileWithoutExtension 
					+ ".gif";
				_e.WriteToFile( fileName );
				
				_d = new GifDecoder( fileName, true );
				_d.Decode();
				
				Assert.AreEqual( ErrorState.Ok, _d.ConsolidatedState, 
				                 actFileWithoutExtension );
				Assert.AreEqual( 1, _d.Frames.Count, actFileWithoutExtension );
				
				if( strategy == ColourTableStrategy.UseGlobal )
				{
					Assert.AreEqual( true, 
					                 _d.LogicalScreenDescriptor.HasGlobalColourTable,
					                 actFileWithoutExtension );
					Assert.IsNotNull( _d.GlobalColourTable, 
					                  actFileWithoutExtension );
				}
				else
				{
					Assert.AreEqual( false, 
					                 _d.LogicalScreenDescriptor.HasGlobalColourTable, 
					                 actFileWithoutExtension );
					Assert.IsNull( _d.GlobalColourTable, actFileWithoutExtension );
				}
				
				string expectedFileName 
					= @"images\Smiley\Smiley"
					+ "-"
					+ actFileWithoutExtension 
					+ ".bmp";
				Image expected = Image.FromFile( expectedFileName );
				ImageAssert.AreEqual( expected, _d.Frames[0].TheImage, expectedFileName );
			}
			
			// now encode a multi-frame animation with a user-supplied palette
			_d = new GifDecoder( @"images\globe\spinning globe better 200px transparent background.gif" );
			_d.Decode();
			_e = new AnimatedGifEncoder();
			_e.QuantizerType = QuantizerType.UseSuppliedPalette;
			_e.Palette = Palette.FromFile( @"ColourTables\C64.act" );
			foreach( GifFrame f in _d.Frames )
			{
				_e.AddFrame( f );
			}
			string globeFileName 
				= "AnimatedGifEncoderTest.UseSuppliedPalette"
				+ globalLocal
				+ ".gif";
			_e.WriteToFile( globeFileName );
			
			_d = new GifDecoder( globeFileName );
			_d.Decode();
			Assert.AreEqual( ErrorState.Ok, _d.ConsolidatedState );
			Assert.AreEqual( _e.Frames.Count, _d.Frames.Count );
		}
		#endregion
		
		#region private methods
		
		#region private TestAnimatedGifEncoder method
		/// <summary>
		/// Tests the AnimatedGifEncoder and the encoded GIF file it produces
		/// using the supplied parameters as property values.
		/// </summary>
		private void TestAnimatedGifEncoder( ColourTableStrategy strategy, 
		                                     int colourQuality, 
		                                     Size logicalScreenSize )
		{
			_e = new AnimatedGifEncoder();
			
			// Check default properties set by constructor.
			Assert.AreEqual( ColourTableStrategy.UseGlobal, 
			                 _e.ColourTableStrategy, 
			                 "Colour table strategy set by constructor" );
			Assert.AreEqual( 10, 
			                 _e.SamplingFactor, 
			                 "Colour quantization quality set by constructor" );
			Assert.AreEqual( Size.Empty, 
			                 _e.LogicalScreenSize, 
			                 "Logical screen size set by constructor" );
			
			_e.ColourTableStrategy = strategy;
			_e.SamplingFactor = colourQuality;
			_e.LogicalScreenSize = logicalScreenSize;
			
			// Check property set/gets
			Assert.AreEqual( strategy, 
			                 _e.ColourTableStrategy, 
			                 "Colour table strategy property set/get" );
			Assert.AreEqual( colourQuality, 
			                 _e.SamplingFactor, 
			                 "Colour quantization quality property set/get" );
			Assert.AreEqual( logicalScreenSize, 
			                 _e.LogicalScreenSize, 
			                 "Logical screen size property get/set" );
			
			foreach( GifFrame thisFrame in _frames )
			{
				_e.AddFrame( thisFrame );
			}
			
			StackTrace t = new StackTrace();
			StackFrame f = t.GetFrame( 1 );
			string fileName 
				= "Checks." + this.GetType().Name 
				+ "." + f.GetMethod().Name + ".gif";
			_e.WriteToFile( fileName );
			
			Stream s = File.OpenRead( fileName );

			// global info
			CheckGifHeader( s );
			bool shouldHaveGlobalColourTable 
				= (strategy == ColourTableStrategy.UseGlobal);
			LogicalScreenDescriptor lsd 
				= CheckLogicalScreenDescriptor( s, shouldHaveGlobalColourTable );
			
			// Only check the global colour table if there should be one
			ColourTable gct = null;
			if( shouldHaveGlobalColourTable )
			{
				gct = CheckColourTable( s, lsd.GlobalColourTableSize );
			}

			CheckExtensionIntroducer( s );
			CheckAppExtensionLabel( s );
			CheckNetscapeExtension( s, 0 );
			
			CheckFrame( s, gct, Bitmap1() );
			CheckFrame( s, gct, Bitmap2() );
			
			// end of image data
			CheckGifTrailer( s );
			CheckEndOfStream( s );
			s.Close();

			// Check the file using the decoder
			_d = new GifDecoder( fileName );
			_d.Decode();
			Assert.AreEqual( ErrorState.Ok, 
			                 _d.ConsolidatedState, 
			                 "Decoder consolidated state" );
			Assert.AreEqual( 2, _d.Frames.Count, "Decoder frame count" );
			Assert.AreEqual( shouldHaveGlobalColourTable, 
			                 _d.LogicalScreenDescriptor.HasGlobalColourTable, 
			                 "Should have global colour table" );
			Assert.AreEqual( logicalScreenSize, 
			                 _d.LogicalScreenDescriptor.LogicalScreenSize, 
			                 "Decoder logical screen size" );
			
			BitmapAssert.AreEqual( Bitmap1(), 
			                       (Bitmap) _d.Frames[0].TheImage, 
			                       "frame 0" );
			BitmapAssert.AreEqual( Bitmap2(), 
			                       (Bitmap) _d.Frames[1].TheImage, 
			                       "frame 1" );
			
			bool shouldHaveLocalColourTable = !shouldHaveGlobalColourTable;
			Assert.AreEqual( shouldHaveLocalColourTable, 
			                 _d.Frames[0].ImageDescriptor.HasLocalColourTable, 
			                 "Frame 0 has local colour table" );
			Assert.AreEqual( shouldHaveLocalColourTable, 
			                 _d.Frames[1].ImageDescriptor.HasLocalColourTable, 
			                 "Frame 0 has local colour table" );
		}
		#endregion
		
		#region private static CheckFrame method
		private static void CheckFrame( Stream s, 
		                                ColourTable globalColourTable,
		                                Bitmap bitmap )
		{
			CheckExtensionIntroducer( s );
			CheckGraphicControlLabel( s );
			CheckGraphicControlExtension( s );
			CheckImageSeparator( s );
			bool shouldHaveLocalColourTable 
				= (globalColourTable == null) ? true : false;
			int lctSizeBits = shouldHaveLocalColourTable ? 1 : 0;
			ImageDescriptor id 
				= CheckImageDescriptor( s, 
				                        shouldHaveLocalColourTable, 
				                        lctSizeBits );
			
			if( globalColourTable == null )
			{
				// no global colour table so must be a local colour table on
				// each frame
				ColourTable lct = CheckColourTable( s, id.LocalColourTableSize );
				CheckImageData( s, lct, id, bitmap );
			}
			else
			{
				CheckImageData( s, globalColourTable, id, bitmap );
			}
			CheckBlockTerminator( s );
		}
		#endregion

		#region private static Bitmap1 method
		private static Bitmap Bitmap1()
		{
			Bitmap bitmap1 = new Bitmap( 2, 2 );
			bitmap1.SetPixel( 0, 0, Color.Black );
			bitmap1.SetPixel( 0, 1, Color.White );
			bitmap1.SetPixel( 1, 0, Color.White );
			bitmap1.SetPixel( 1, 1, Color.Black );
			return bitmap1;
		}
		#endregion
		
		#region private static Bitmap2 method
		private static Bitmap Bitmap2()
		{
			Bitmap bitmap2 = new Bitmap( 2, 2 );
			bitmap2.SetPixel( 0, 0, Color.White );
			bitmap2.SetPixel( 0, 1, Color.Black );
			bitmap2.SetPixel( 1, 0, Color.Black );
			bitmap2.SetPixel( 1, 1, Color.White );
			return bitmap2;
		}
		#endregion

		#region private static CheckGifHeader method
		private static void CheckGifHeader( Stream s )
		{
			// check GIF header
			GifHeader gh = new GifHeader( s );
			Assert.AreEqual( ErrorState.Ok, gh.ConsolidatedState );
			Assert.AreEqual( "GIF", gh.Signature );
			Assert.AreEqual( "89a", gh.Version );
		}
		#endregion
		
		#region private static CheckLogicalScreenDescriptor method
		private static LogicalScreenDescriptor
			CheckLogicalScreenDescriptor( Stream s,
			                              bool shouldHaveGlobalColourTable )
		{
			// check logical screen descriptor
			LogicalScreenDescriptor lsd = new LogicalScreenDescriptor( s );
			Assert.AreEqual( ErrorState.Ok, 
			                 lsd.ConsolidatedState,
			                 "Logical screen descriptor consolidated state" );
			Assert.AreEqual( new Size( 2, 2 ), 
			                 lsd.LogicalScreenSize, 
			                 "Logical screen size" );
			Assert.AreEqual( shouldHaveGlobalColourTable, 
			                 lsd.HasGlobalColourTable,
			                 "Should have global colour table" );
			Assert.AreEqual( false, 
			                 lsd.GlobalColourTableIsSorted, 
			                 "Global colour table is sorted" );
			return lsd;
		}
		#endregion
		
		#region private static CheckColourTable method
		private static ColourTable CheckColourTable( Stream s, 
		                                             int colourTableSize )
		{
			// read colour table
			ColourTable ct = new ColourTable( s, colourTableSize );
			Assert.AreEqual( ErrorState.Ok, ct.ConsolidatedState );
			return ct;
		}
		#endregion
		
		#region private static CheckExtensionIntroducer method
		private static void CheckExtensionIntroducer( Stream s )
		{
			// check for extension introducer
			int code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeExtensionIntroducer, code );
		}
		#endregion
		
		#region private static CheckAppExtensionLabel method
		private static void CheckAppExtensionLabel( Stream s )
		{
			// check for app extension label
			int code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeApplicationExtensionLabel, code );
		}
		#endregion
		
		#region private static CheckNetscapeExtension method
		private static void CheckNetscapeExtension( Stream s, int loopCount )
		{
			// check netscape extension
			ApplicationExtension ae = new ApplicationExtension( s );
			Assert.AreEqual( ErrorState.Ok, ae.ConsolidatedState );
			NetscapeExtension ne = new NetscapeExtension( ae );
			Assert.AreEqual( ErrorState.Ok, ne.ConsolidatedState );
			Assert.AreEqual( loopCount, ne.LoopCount );
		}
		#endregion
		
		#region private static CheckGraphicControlLabel method
		private static void CheckGraphicControlLabel( Stream s )
		{
			// check for gce label
			int code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeGraphicControlLabel, code );
		}
		#endregion
		
		#region private static CheckGraphicControlExtension methods
		private static void CheckGraphicControlExtension( Stream s )
		{
			CheckGraphicControlExtension( s, Color.Empty );
		}
		
		private static void CheckGraphicControlExtension( Stream s, 
		                                                  Color transparentColour )
		{
			// check graphic control extension
			GraphicControlExtension gce = new GraphicControlExtension( s );
			Assert.AreEqual( ErrorState.Ok, gce.ConsolidatedState );
			Assert.AreEqual( 10, gce.DelayTime );
			if( transparentColour == Color.Empty )
			{
				Assert.AreEqual( 0, gce.TransparentColourIndex );
				Assert.AreEqual( false, gce.HasTransparentColour );
				Assert.AreEqual( DisposalMethod.DoNotDispose, gce.DisposalMethod );
			}
			else
			{
				Assert.AreEqual( true, gce.HasTransparentColour );
				// TODO: a way to check the transparent colour index?
				Assert.AreEqual( DisposalMethod.RestoreToBackgroundColour, 
				                 gce.DisposalMethod );
			}
			Assert.AreEqual( false, gce.ExpectsUserInput );
		}
		#endregion
		
		#region private static CheckImageSeparator method
		private static void CheckImageSeparator( Stream s )
		{
			// check for image separator
			int code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeImageSeparator, code );
		}
		#endregion
		
		#region private static CheckImageDescriptor method
		private static ImageDescriptor 
			CheckImageDescriptor( Stream s, 
			                      bool shouldHaveLocalColourTable,
			                      int localColourTableSizeBits )
		{
			// check for image descriptor
			ImageDescriptor id = new ImageDescriptor( s );
			Assert.AreEqual( ErrorState.Ok, 
			                 id.ConsolidatedState, 
			                 "Image descriptor consolidated state" );
			Assert.AreEqual( shouldHaveLocalColourTable, 
			                 id.HasLocalColourTable, 
			                 "Should have local colour table" );
			Assert.AreEqual( false, id.IsInterlaced, "Is interlaced" );
			Assert.AreEqual( false, id.IsSorted, "Local colour table is sorted" );
			Assert.AreEqual( localColourTableSizeBits, 
			                 id.LocalColourTableSizeBits, 
			                 "Local colour table size (bits)" );
			Assert.AreEqual( 1 << (localColourTableSizeBits + 1), 
			                 id.LocalColourTableSize,
			                 "Local colour table size");
			Assert.AreEqual( new Size( 2, 2 ), id.Size, "Image descriptor size" );
			Assert.AreEqual( new Point( 0, 0 ), 
			                 id.Position, 
			                 "Image descriptor position" );
			return id;
		}
		#endregion
		
		#region private static CheckImageData method
		private static void CheckImageData( Stream s, 
		                                    ColourTable act, 
		                                    ImageDescriptor id, 
		                                    Bitmap expectedBitmap )
		{
			// read, decode and check image data
			// Cannot compare encoded LZW data directly as different encoders
			// will create different colour tables, so even if the bitmaps are
			// identical, the colour indices will be different
			int pixelCount = id.Size.Width * id.Size.Height;
			TableBasedImageData tbid = new TableBasedImageData( s, pixelCount );
			Assert.AreEqual( ErrorState.Ok, tbid.ConsolidatedState );
			for( int y = 0; y < id.Size.Height; y++ )
			{
				for( int x = 0; x < id.Size.Width; x++ )
				{
					int i = (y * id.Size.Width) + x;
					Assert.AreEqual( expectedBitmap.GetPixel( x, y ),
					                 act[tbid.Pixels[i]],
					                 "X: " + x + ", Y: " + y );
				}
			}
		}
		#endregion
		
		#region private static void CheckBlockTerminator method
		private static void CheckBlockTerminator( Stream s )
		{
			// Check for block terminator after image data
			int code = ExampleComponent.CallRead( s );
			Assert.AreEqual( 0x00, code );
		}
		#endregion
		
		#region private static void CheckGifTrailer method
		private static void CheckGifTrailer( Stream s )
		{
			// check for GIF trailer
			int code = ExampleComponent.CallRead( s );
			Assert.AreEqual( GifComponent.CodeTrailer, code );
		}
		#endregion
		
		#region private static void CheckEndOfStream method
		private static void CheckEndOfStream( Stream s )
		{
			// check we're at the end of the stream
			int code = ExampleComponent.CallRead( s );
			Assert.AreEqual( -1, code );
		}
		#endregion
		
		#endregion

		#region Test cases to reproduce reported bugs
		
		#region Bug2892015
		/// <summary>
		/// Here are the steps to reproduce the problem :
		/// Lets add 3 frames, and encode a gif. 
		/// On my machine it takes 2 or 3 seconds. -> great 
		/// Now, i add 50 frames and encode a new gif -> it works, and it takes 
		/// around 50-60 seconds. 
		/// And finaly, i remove all the frames using the _frames.Clear() 
		/// method, and add 3 frames again.
		/// When i encode the new 3 frames gif, it takes  more than 60 seconds !
		/// </summary>
		[Test]
		public void Bug2892015()
		{
			ReportStart();
			_e = new AnimatedGifEncoder();
			
			#region create 10 random bitmaps
			Collection<Bitmap> bitmaps = new Collection<Bitmap>();
			for( int i = 0; i < 10; i++ )
			{
				Bitmap bitmap = RandomBitmap.Create( new Size( 50, 50 ),
				                                     10,
				                                     PixelFormat.Format32bppRgb );
				bitmaps.Add( bitmap );
			}
			#endregion
			
			DateTime startTime;
			DateTime endTime;
			
			#region create animation using just the first 3 (this should be quick)
			for( int i = 0; i < 3; i++ )
			{
				_e.AddFrame( new GifFrame( bitmaps[i] ) );
			}
			
			startTime = DateTime.Now;
			_e.WriteToFile( "2892015-1.gif" );
			endTime = DateTime.Now;
			TimeSpan runTime1 = endTime - startTime;
			WriteMessage( "Encoding 3 frames took " + runTime1 );
			#endregion
			
			_e.Frames.Clear();
			
			#region create animation using all the bitmaps (this will take longer)
			foreach( Bitmap bitmap in bitmaps )
			{
				_e.AddFrame( new GifFrame( bitmap ) );
			}
			
			startTime = DateTime.Now;
			_e.WriteToFile( "2892015-2.gif" );
			endTime = DateTime.Now;
			TimeSpan runTime2 = endTime - startTime;
			WriteMessage( "Encoding all " + bitmaps.Count + " frames took " + runTime2 );
			#endregion
			
			_e.Frames.Clear();
			
			#region create animation using just the first 3 (this should be quick)
			for( int i = 0; i < 3; i++ )
			{
				_e.AddFrame( new GifFrame( bitmaps[i] ) );
			}
			
			startTime = DateTime.Now;
			_e.WriteToFile( "2892015-3.gif" );
			endTime = DateTime.Now;
			TimeSpan runTime3 = endTime - startTime;
			WriteMessage( "Encoding 3 frames took " + runTime3 );
			#endregion
			
			Assert.IsTrue( runTime3 < runTime2 );
			_d = new GifDecoder( "2892015-3.gif" );
			_d.Decode();
			Assert.AreEqual( 3, _d.Frames.Count );
			
			ReportEnd();
		}
		#endregion
		
		#region BadEncodingUsingGameboyPaletteLocal
		/// <summary>
		/// Reproduces the problem whereby using the gameboy.act as a 
		/// user-supplied palette to encode a GIF with local colour tables,
		/// results in a corrupt GIF.
		/// Solution: when building the local colour table from the .act file
		/// in AnimatedGifEncoder.SetActiveColourTable, remember to pad the
		/// colour table out to a number of colours which is a whole power of 2.
		/// </summary>
		[Test]
		[SuppressMessage("Microsoft.Naming", 
		                 "CA1704:IdentifiersShouldBeSpelledCorrectly", 
		                 MessageId = "Gameboy")]
		public void BadEncodingUsingGameboyPaletteLocal()
		{
			ReportStart();
			_e = new AnimatedGifEncoder();
			_e.ColourTableStrategy = ColourTableStrategy.UseLocal;
			_e.QuantizerType = QuantizerType.UseSuppliedPalette;
			
			GifFrame frame 
				= new GifFrame( Image.FromFile( @"images\smiley.bmp" ) );
			_e.AddFrame( frame );
			
			Assert.AreEqual( ColourTableStrategy.UseLocal, _e.ColourTableStrategy );
			Assert.AreEqual( QuantizerType.UseSuppliedPalette, _e.QuantizerType );
			
			frame.Palette = Palette.FromFile( @"ColourTables\gameboy.act" );
			
			Assert.AreEqual( ColourTableStrategy.UseLocal, _e.ColourTableStrategy );
			Assert.AreEqual( QuantizerType.UseSuppliedPalette, _e.QuantizerType );
			
			_e.WriteToFile( GifFileName );
			
			_d = new GifDecoder( GifFileName );
			_d.Decode();
			
			Assert.AreEqual( ErrorState.Ok, _d.ConsolidatedState );
			
			ReportEnd();
		}
		#endregion
		
		#endregion

		#region IDisposable implementation
		/// <summary>
		/// Indicates whether or not the Dispose( bool ) method has already been 
		/// called.
		/// </summary>
		bool _disposed;

		/// <summary>
		/// Finalzer.
		/// </summary>
		~AnimatedGifEncoderTest()
		{
			Dispose( false );
		}

		/// <summary>
		/// Disposes resources used by this class.
		/// </summary>
		public void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}

		/// <summary>
		/// Disposes resources used by this class.
		/// </summary>
		/// <param name="disposing">
		/// Indicates whether this method is being called by the class's Dispose
		/// method (true) or by the garbage collector (false).
		/// </param>
		protected virtual void Dispose( bool disposing )
		{
			if( !_disposed )
			{
				if( disposing )
				{
					// dispose-only, i.e. non-finalizable logic
					_d.Dispose();
					_e.Dispose();
				}

				// new shared cleanup logic
				_disposed = true;
			}

			// Uncomment if the base type also implements IDisposable
//			base.Dispose( disposing );
		}
		#endregion
	}
}
