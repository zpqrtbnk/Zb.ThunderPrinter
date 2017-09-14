using iText.Kernel.Colors;
using iText.Layout.Element;
using iText.Layout.Renderer;

namespace ThunderPrint
{
    public class StrikedCellRenderer : CellRenderer
    {
        private static Color Color = new DeviceCmyk(0, 0, 0, 40);

        public StrikedCellRenderer(Cell modelElement) 
            : base(modelElement)
        { }

        public override void Draw(DrawContext drawContext)
        {
            base.Draw(drawContext);

            var color = drawContext.GetCanvas().GetGraphicsState().GetStrokeColor();

            drawContext.GetCanvas()

                // not sure how to change colors without impacting text etc?
                //.SetLineWidth(10)
                //.SetStrokeColor(Color.RED) // changes the TEXT color so its... kinda bad
                //.SetFillColor(Color.RED)

                .SetLineWidth(.5f)
                .SetStrokeColor(Color)

                // this works
                .MoveTo(GetBorderAreaBBox().GetLeft(), GetBorderAreaBBox().GetBottom())
                .LineTo(GetBorderAreaBBox().GetRight(), GetBorderAreaBBox().GetTop())
                .Stroke()
                
                // restore
                .SetStrokeColor(color);

            //var canvas = new PdfCanvas(drawContext.GetDocument(),);

            //var aboveCanvas = new PdfCanvas(drawContext.GetDocument().GetLastPage().NewContentStreamAfter(),
            //    drawContext.GetDocument().GetLastPage().GetResources(), drawContext.GetDocument());

            // this works...
            //new Canvas(aboveCanvas, drawContext.GetDocument(), GetOccupiedAreaBBox())
            //    .Add(new Paragraph("ARF")
            //        .SetBackgroundColor(Color.LIGHT_GRAY)
            //        .SetFixedPosition(GetOccupiedAreaBBox().GetLeft() + 5, GetOccupiedAreaBBox().GetTop() - 8, 30));

            //new Canvas(aboveCanvas, drawContext.GetDocument(), GetOccupiedAreaBBox())
            //    .SetStrokeColor(Color.BLACK)
            //    .MoveTo(0, 100)
            //    .LineTo(100, 0);

            //aboveCanvas
            //    .SetStrokeColor(Color.BLACK)
            //    .MoveTo(0, 100)
            //    .LineTo(100, 0);

            //var canvas = new Canvas();

            //float x1, y1, x2, y2;

            //canvas.Add(new Line(x1, y1, x2, y2));
        }
    }
}
