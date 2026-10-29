// Generated from Stitch project5482405501300014434, screen cb5ca87bcb5149d48ceb3c7e951b8683.
// Edit scripts/import-stitch-icons.py or source SVG, not individual drawing calls.
namespace Liangtu;
internal static class ThemeIconDrawings
{
 static Brush B(string hex) { var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();return b; }
 static Pen P(string hex,double width,bool round) { var p=new Pen(B(hex),width);if(round)p.StartLineCap=p.EndLineCap=PenLineCap.Round;p.Freeze();return p; }
 internal static (DrawingGroup drawing,double units) Create(string key)
 {
  var group=new DrawingGroup();double units;using(var dc=group.Open()) {
   switch(key) {
    case "linen-app":
     units=256;
     dc.DrawRoundedRectangle(B("#F5EEDC"),null,new Rect(0,0,256,256),48,48);
     dc.DrawGeometry(null,P("#8E785C",4.5,true),Geometry.Parse("F1 M24 72 Q 128 108 232 72"));
     dc.DrawGeometry(B("#754823"),null,Geometry.Parse("F1 M121,50 L133,52 L129,94 L117,92 Z"));
     dc.PushTransform(new RotateTransform(5,128,148));
     dc.DrawRoundedRectangle(B("#FFFDF8"),null,new Rect(66,78,124,136),6,6);
     dc.DrawRoundedRectangle(B("#DF8A6D"),null,new Rect(76,88,104,96),3,3);
     dc.DrawEllipse(B("#FAD06C"),null,new Point(152,114),13,13);
     dc.DrawGeometry(B("#C66D52"),null,Geometry.Parse("F1 M76,184 L116,134 L148,184 Z"));
     dc.DrawGeometry(B("#6B7955"),null,Geometry.Parse("F1 M112,184 L146,144 L180,184 Z"));
     dc.Pop();
     dc.DrawGeometry(B("#A87448"),null,Geometry.Parse("F1 M125,52 L139,55 L133,106 L119,102 Z"));
     dc.DrawGeometry(B("#915E34"),null,Geometry.Parse("F1 M119,102 L125,52 L122,54 L116,101 Z"));
     dc.PushTransform(new RotateTransform(13,129,75.5));
     dc.DrawRoundedRectangle(B("#4A3F35"),null,new Rect(122,72,14,7),1.5,1.5);
     dc.Pop();
     dc.DrawLine(P("#4A3F35",2.5,true),new Point(120,102),new Point(134,106));
     break;
    case "linen-small":
     units=32;
     dc.DrawRoundedRectangle(B("#F5EEDC"),null,new Rect(0,0,32,32),6,6);
     dc.DrawGeometry(null,P("#8E785C",1.2,true),Geometry.Parse("F1 M3 9 Q 16 13 29 9"));
     dc.PushTransform(new RotateTransform(5,16,18.5));
     dc.DrawRoundedRectangle(B("#FFFDF8"),null,new Rect(8,10,16,17),1.5,1.5);
     dc.DrawRoundedRectangle(B("#DF8A6D"),null,new Rect(9.5,11.5,13,12),0.5,0.5);
     dc.DrawEllipse(B("#FAD06C"),null,new Point(19,14.5),1.6,1.6);
     dc.DrawGeometry(B("#C66D52"),null,Geometry.Parse("F1 M9.5,23.5 L14,17.5 L18,23.5 Z"));
     dc.DrawGeometry(B("#6B7955"),null,Geometry.Parse("F1 M14,23.5 L18.5,18.5 L22.5,23.5 Z"));
     dc.Pop();
     dc.PushTransform(new RotateTransform(8,15.6,9.75));
     dc.DrawRoundedRectangle(B("#A87448"),null,new Rect(14,6,3.2,7.5),0.6,0.6);
     dc.Pop();
     dc.PushTransform(new RotateTransform(8,16,9.5));
     dc.DrawRoundedRectangle(B("#4A3F35"),null,new Rect(14.5,8.8,3,1.4),0.3,0.3);
     dc.Pop();
     break;
    case "linen-tray-light-16":
     units=16;
     dc.DrawGeometry(null,P("#3A2D1F",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#3A2D1F",1.2,false),new Rect(3.5,4,9,10),1,1);
     dc.Pop();
     dc.DrawEllipse(B("#C66D52"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#475436"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#8A4E24"),P("#3A2D1F",0.8,false),new Rect(7,1.5,2.4,5),0.5,0.5);
     dc.Pop();
     break;
    case "linen-tray-light-20":
     units=20;
     dc.DrawGeometry(null,P("#3A2D1F",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#3A2D1F",1.4,false),new Rect(4.5,5,11,12.5),1.2,1.2);
     dc.Pop();
     dc.DrawEllipse(B("#C66D52"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#475436"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#8A4E24"),P("#3A2D1F",1,false),new Rect(8.8,2,2.8,6.2),0.6,0.6);
     dc.Pop();
     break;
    case "linen-tray-dark-16":
     units=16;
     dc.DrawGeometry(null,P("#F5EEDC",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#2E2419"),P("#F5EEDC",1.2,false),new Rect(3.5,4,9,10),1,1);
     dc.Pop();
     dc.DrawEllipse(B("#FAD06C"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#8DA070"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#DF8A6D"),P("#F5EEDC",0.8,false),new Rect(7,1.5,2.4,5),0.5,0.5);
     dc.Pop();
     break;
    case "linen-tray-dark-20":
     units=20;
     dc.DrawGeometry(null,P("#F5EEDC",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#2E2419"),P("#F5EEDC",1.4,false),new Rect(4.5,5,11,12.5),1.2,1.2);
     dc.Pop();
     dc.DrawEllipse(B("#FAD06C"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#8DA070"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#DF8A6D"),P("#F5EEDC",1,false),new Rect(8.8,2,2.8,6.2),0.6,0.6);
     dc.Pop();
     break;
    case "color-play-app":
     units=256;
     dc.DrawRoundedRectangle(B("#FAF9F5"),null,new Rect(0,0,256,256),48,48);
     dc.DrawGeometry(null,P("#B0C8BC",4.5,true),Geometry.Parse("F1 M24 72 Q 128 108 232 72"));
     dc.DrawGeometry(B("#527F6C"),null,Geometry.Parse("F1 M121,50 L133,52 L129,94 L117,92 Z"));
     dc.PushTransform(new RotateTransform(5,128,148));
     dc.DrawRoundedRectangle(B("#FFFFFF"),null,new Rect(66,78,124,136),10,10);
     dc.DrawRoundedRectangle(B("#A7D5FA"),null,new Rect(76,88,104,96),6,6);
     dc.DrawEllipse(B("#FCE182"),null,new Point(152,114),14,14);
     dc.DrawGeometry(B("#F8BCAC"),null,Geometry.Parse("F1 M76,184 L116,132 L148,184 Z"));
     dc.DrawGeometry(B("#E89A88"),null,Geometry.Parse("F1 M112,184 L146,140 L180,184 Z"));
     dc.Pop();
     dc.DrawGeometry(B("#71A890"),null,Geometry.Parse("F1 M125,52 L139,55 L133,106 L119,102 Z"));
     dc.DrawGeometry(B("#5E907B"),null,Geometry.Parse("F1 M119,102 L125,52 L122,54 L116,101 Z"));
     dc.DrawEllipse(B("#FCE182"),null,new Point(127,76),3.5,3.5);
     dc.DrawLine(P("#416958",2.5,true),new Point(120,102),new Point(134,106));
     break;
    case "color-play-small":
     units=32;
     dc.DrawRoundedRectangle(B("#FAF9F5"),null,new Rect(0,0,32,32),6,6);
     dc.DrawGeometry(null,P("#B0C8BC",1.2,true),Geometry.Parse("F1 M3 9 Q 16 13 29 9"));
     dc.PushTransform(new RotateTransform(5,16,18.5));
     dc.DrawRoundedRectangle(B("#FFFFFF"),null,new Rect(8,10,16,17),2,2);
     dc.DrawRoundedRectangle(B("#A7D5FA"),null,new Rect(9.5,11.5,13,12),1,1);
     dc.DrawEllipse(B("#FCE182"),null,new Point(19,14.5),1.6,1.6);
     dc.DrawGeometry(B("#F8BCAC"),null,Geometry.Parse("F1 M9.5,23.5 L14,17.5 L18,23.5 Z"));
     dc.DrawGeometry(B("#E89A88"),null,Geometry.Parse("F1 M14,23.5 L18.5,18.5 L22.5,23.5 Z"));
     dc.Pop();
     dc.PushTransform(new RotateTransform(8,15.6,9.75));
     dc.DrawRoundedRectangle(B("#71A890"),null,new Rect(14,6,3.2,7.5),0.8,0.8);
     dc.Pop();
     dc.DrawEllipse(B("#FCE182"),null,new Point(15.8,9.8),0.7,0.7);
     break;
    case "color-play-tray-light-16":
     units=16;
     dc.DrawGeometry(null,P("#234234",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#234234",1.2,false),new Rect(3.5,4,9,10),1.2,1.2);
     dc.Pop();
     dc.DrawEllipse(B("#F8B195"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#5F937E"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#71A890"),P("#234234",0.8,false),new Rect(7,1.5,2.4,5),0.6,0.6);
     dc.Pop();
     break;
    case "color-play-tray-light-20":
     units=20;
     dc.DrawGeometry(null,P("#234234",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#234234",1.4,false),new Rect(4.5,5,11,12.5),1.4,1.4);
     dc.Pop();
     dc.DrawEllipse(B("#F8B195"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#5F937E"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#71A890"),P("#234234",1,false),new Rect(8.8,2,2.8,6.2),0.8,0.8);
     dc.Pop();
     break;
    case "color-play-tray-dark-16":
     units=16;
     dc.DrawGeometry(null,P("#FFFFFF",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#1C2E26"),P("#FFFFFF",1.2,false),new Rect(3.5,4,9,10),1.2,1.2);
     dc.Pop();
     dc.DrawEllipse(B("#FCE182"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#71A890"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#F8BCAC"),P("#FFFFFF",0.8,false),new Rect(7,1.5,2.4,5),0.6,0.6);
     dc.Pop();
     break;
    case "color-play-tray-dark-20":
     units=20;
     dc.DrawGeometry(null,P("#FFFFFF",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#1C2E26"),P("#FFFFFF",1.4,false),new Rect(4.5,5,11,12.5),1.4,1.4);
     dc.Pop();
     dc.DrawEllipse(B("#FCE182"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#71A890"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#F8BCAC"),P("#FFFFFF",1,false),new Rect(8.8,2,2.8,6.2),0.8,0.8);
     dc.Pop();
     break;
    case "porcelain-app":
     units=256;
     dc.DrawRoundedRectangle(B("#ECF2F7"),null,new Rect(0,0,256,256),48,48);
     dc.DrawGeometry(null,P("#8C9CAE",4.5,true),Geometry.Parse("F1 M24 72 Q 128 108 232 72"));
     dc.DrawGeometry(B("#697A8C"),null,Geometry.Parse("F1 M121,50 L133,52 L129,94 L117,92 Z"));
     dc.PushTransform(new RotateTransform(5,128,148));
     dc.DrawRoundedRectangle(B("#FFFFFF"),null,new Rect(66,78,124,136),4,4);
     dc.DrawRoundedRectangle(B("#D7E6F2"),null,new Rect(76,88,104,96),2,2);
     dc.DrawEllipse(B("#FFE58F"),null,new Point(152,114),13,13);
     dc.DrawGeometry(B("#7FA4C2"),null,Geometry.Parse("F1 M76,184 L116,134 L148,184 Z"));
     dc.DrawGeometry(B("#4B6E8C"),null,Geometry.Parse("F1 M112,184 L146,144 L180,184 Z"));
     dc.Pop();
     dc.DrawGeometry(B("#B3C2D1"),null,Geometry.Parse("F1 M125,52 L139,55 L133,106 L119,102 Z"));
     dc.DrawGeometry(B("#9BAAB9"),null,Geometry.Parse("F1 M119,102 L125,52 L122,54 L116,101 Z"));
     dc.PushTransform(new RotateTransform(13,129,75.25));
     dc.DrawRoundedRectangle(B("#3D4B59"),null,new Rect(122,72,14,6.5),1,1);
     dc.Pop();
     dc.DrawLine(P("#3D4B59",2.5,true),new Point(120,102),new Point(134,106));
     break;
    case "porcelain-small":
     units=32;
     dc.DrawRoundedRectangle(B("#ECF2F7"),null,new Rect(0,0,32,32),6,6);
     dc.DrawGeometry(null,P("#8C9CAE",1.2,true),Geometry.Parse("F1 M3 9 Q 16 13 29 9"));
     dc.PushTransform(new RotateTransform(5,16,18.5));
     dc.DrawRoundedRectangle(B("#FFFFFF"),null,new Rect(8,10,16,17),1.2,1.2);
     dc.DrawRoundedRectangle(B("#D7E6F2"),null,new Rect(9.5,11.5,13,12),0.5,0.5);
     dc.DrawEllipse(B("#FFE58F"),null,new Point(19,14.5),1.6,1.6);
     dc.DrawGeometry(B("#7FA4C2"),null,Geometry.Parse("F1 M9.5,23.5 L14,17.5 L18,23.5 Z"));
     dc.DrawGeometry(B("#4B6E8C"),null,Geometry.Parse("F1 M14,23.5 L18.5,18.5 L22.5,23.5 Z"));
     dc.Pop();
     dc.PushTransform(new RotateTransform(8,15.6,9.75));
     dc.DrawRoundedRectangle(B("#B3C2D1"),null,new Rect(14,6,3.2,7.5),0.5,0.5);
     dc.Pop();
     dc.PushTransform(new RotateTransform(8,16,9.5));
     dc.DrawRoundedRectangle(B("#3D4B59"),null,new Rect(14.5,8.8,3,1.4),0.2,0.2);
     dc.Pop();
     break;
    case "porcelain-tray-light-16":
     units=16;
     dc.DrawGeometry(null,P("#1E293B",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#1E293B",1.2,false),new Rect(3.5,4,9,10),0.8,0.8);
     dc.Pop();
     dc.DrawEllipse(B("#EAB308"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#4B6E8C"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#94A3B8"),P("#1E293B",0.8,false),new Rect(7,1.5,2.4,5),0.4,0.4);
     dc.Pop();
     break;
    case "porcelain-tray-light-20":
     units=20;
     dc.DrawGeometry(null,P("#1E293B",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#1E293B",1.4,false),new Rect(4.5,5,11,12.5),1,1);
     dc.Pop();
     dc.DrawEllipse(B("#EAB308"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#4B6E8C"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#94A3B8"),P("#1E293B",1,false),new Rect(8.8,2,2.8,6.2),0.5,0.5);
     dc.Pop();
     break;
    case "porcelain-tray-dark-16":
     units=16;
     dc.DrawGeometry(null,P("#F1F5F9",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#1E293B"),P("#F1F5F9",1.2,false),new Rect(3.5,4,9,10),0.8,0.8);
     dc.Pop();
     dc.DrawEllipse(B("#FDE047"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#7BB1D9"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#CBD5E1"),P("#F1F5F9",0.8,false),new Rect(7,1.5,2.4,5),0.4,0.4);
     dc.Pop();
     break;
    case "porcelain-tray-dark-20":
     units=20;
     dc.DrawGeometry(null,P("#F1F5F9",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#1E293B"),P("#F1F5F9",1.4,false),new Rect(4.5,5,11,12.5),1,1);
     dc.Pop();
     dc.DrawEllipse(B("#FDE047"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#7BB1D9"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#CBD5E1"),P("#F1F5F9",1,false),new Rect(8.8,2,2.8,6.2),0.5,0.5);
     dc.Pop();
     break;
    case "darkroom-app":
     units=256;
     dc.DrawRoundedRectangle(B("#1C2128"),null,new Rect(0,0,256,256),48,48);
     dc.DrawGeometry(null,P("#485363",4.5,true),Geometry.Parse("F1 M24 72 Q 128 108 232 72"));
     dc.DrawGeometry(B("#3E4752"),null,Geometry.Parse("F1 M121,50 L133,52 L129,94 L117,92 Z"));
     dc.PushTransform(new RotateTransform(5,128,148));
     dc.DrawRoundedRectangle(B("#E8EDF2"),null,new Rect(66,78,124,136),5,5);
     dc.DrawRoundedRectangle(B("#2E1114"),null,new Rect(76,88,104,96),2,2);
     dc.DrawEllipse(B("#F59E0B"),null,new Point(152,114),13,13);
     dc.DrawGeometry(B("#691B1F"),null,Geometry.Parse("F1 M76,184 L116,134 L148,184 Z"));
     dc.DrawGeometry(B("#14080A"),null,Geometry.Parse("F1 M112,184 L146,144 L180,184 Z"));
     dc.Pop();
     dc.DrawGeometry(B("#7E8B99"),null,Geometry.Parse("F1 M125,52 L139,55 L133,106 L119,102 Z"));
     dc.DrawGeometry(B("#626E7A"),null,Geometry.Parse("F1 M119,102 L125,52 L122,54 L116,101 Z"));
     dc.DrawEllipse(B("#F59E0B"),null,new Point(127,76),3.5,3.5);
     dc.DrawLine(P("#1C2128",2.5,true),new Point(120,102),new Point(134,106));
     break;
    case "darkroom-small":
     units=32;
     dc.DrawRoundedRectangle(B("#1C2128"),null,new Rect(0,0,32,32),6,6);
     dc.DrawGeometry(null,P("#485363",1.2,true),Geometry.Parse("F1 M3 9 Q 16 13 29 9"));
     dc.PushTransform(new RotateTransform(5,16,18.5));
     dc.DrawRoundedRectangle(B("#E8EDF2"),null,new Rect(8,10,16,17),1.2,1.2);
     dc.DrawRoundedRectangle(B("#2E1114"),null,new Rect(9.5,11.5,13,12),0.5,0.5);
     dc.DrawEllipse(B("#F59E0B"),null,new Point(19,14.5),1.6,1.6);
     dc.DrawGeometry(B("#691B1F"),null,Geometry.Parse("F1 M9.5,23.5 L14,17.5 L18,23.5 Z"));
     dc.DrawGeometry(B("#14080A"),null,Geometry.Parse("F1 M14,23.5 L18.5,18.5 L22.5,23.5 Z"));
     dc.Pop();
     dc.PushTransform(new RotateTransform(8,15.6,9.75));
     dc.DrawRoundedRectangle(B("#7E8B99"),null,new Rect(14,6,3.2,7.5),0.6,0.6);
     dc.Pop();
     dc.DrawEllipse(B("#F59E0B"),null,new Point(15.8,9.8),0.7,0.7);
     break;
    case "darkroom-tray-light-16":
     units=16;
     dc.DrawGeometry(null,P("#0F172A",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#0F172A",1.2,false),new Rect(3.5,4,9,10),0.8,0.8);
     dc.Pop();
     dc.DrawEllipse(B("#D97706"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#0F172A"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#64748B"),P("#0F172A",0.8,false),new Rect(7,1.5,2.4,5),0.5,0.5);
     dc.Pop();
     break;
    case "darkroom-tray-light-20":
     units=20;
     dc.DrawGeometry(null,P("#0F172A",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#FFFFFF"),P("#0F172A",1.4,false),new Rect(4.5,5,11,12.5),1,1);
     dc.Pop();
     dc.DrawEllipse(B("#D97706"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#0F172A"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#64748B"),P("#0F172A",1,false),new Rect(8.8,2,2.8,6.2),0.6,0.6);
     dc.Pop();
     break;
    case "darkroom-tray-dark-16":
     units=16;
     dc.DrawGeometry(null,P("#E2E8F0",1.5,true),Geometry.Parse("F1 M1 4.5 Q 8 7 15 4.5"));
     dc.PushTransform(new RotateTransform(6,8,9));
     dc.DrawRoundedRectangle(B("#1C2026"),P("#E2E8F0",1.2,false),new Rect(3.5,4,9,10),0.8,0.8);
     dc.Pop();
     dc.DrawEllipse(B("#F59E0B"),null,new Point(9.5,7.2),1.1,1.1);
     dc.DrawGeometry(B("#E2E8F0"),null,Geometry.Parse("F1 M5.5,12 L8.5,8.8 L11.5,12 Z"));
     dc.PushTransform(new RotateTransform(10,8.2,4));
     dc.DrawRoundedRectangle(B("#F59E0B"),P("#FFFFFF",0.8,false),new Rect(7,1.5,2.4,5),0.5,0.5);
     dc.Pop();
     break;
    case "darkroom-tray-dark-20":
     units=20;
     dc.DrawGeometry(null,P("#E2E8F0",1.75,true),Geometry.Parse("F1 M1 5.5 Q 10 8.5 19 5.5"));
     dc.PushTransform(new RotateTransform(6,10,11.2));
     dc.DrawRoundedRectangle(B("#1C2026"),P("#E2E8F0",1.4,false),new Rect(4.5,5,11,12.5),1,1);
     dc.Pop();
     dc.DrawEllipse(B("#F59E0B"),null,new Point(12,9),1.3,1.3);
     dc.DrawGeometry(B("#E2E8F0"),null,Geometry.Parse("F1 M7,15.5 L10.5,11.5 L14.5,15.5 Z"));
     dc.PushTransform(new RotateTransform(10,10.2,5.1));
     dc.DrawRoundedRectangle(B("#F59E0B"),P("#FFFFFF",1,false),new Rect(8.8,2,2.8,6.2),0.6,0.6);
     dc.Pop();
     break;
    default:throw new ArgumentOutOfRangeException(nameof(key));
   }
  }
  group.Freeze();return(group,units);
 }
}
