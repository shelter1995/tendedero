using System.Security.Cryptography;
using System.Text.Json;
internal static class Program {
 [STAThread] static void Main(string[] args){
  ApplicationConfiguration.Initialize();
  var folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
  var form=new Form{Text="晾图验收 · 图片接收应用",Width=700,Height=500,StartPosition=FormStartPosition.Manual,Location=new Point(900,250),AllowDrop=true};
  var label=new Label{Dock=DockStyle.Top,Height=55,Text="把测试卡片拖到这里：只接收副本，原文件应保留",TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",12)};
  var image=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(230,234,239)};
  form.Controls.Add(image);form.Controls.Add(label);
  form.DragEnter+=(_,e)=>e.Effect=e.Data?.GetDataPresent(DataFormats.FileDrop)==true?DragDropEffects.Copy:DragDropEffects.None;
  form.DragDrop+=(_,e)=>{
   var files=(string[]?)e.Data?.GetData(DataFormats.FileDrop);if(files==null||files.Length==0)return;
   using var fileImage=Image.FromFile(files[0]);var copy=new Bitmap(fileImage);image.Image?.Dispose();image.Image=copy;
   var bitmap=e.Data?.GetData(DataFormats.Bitmap) as Bitmap;
   var record=new{Effect="Copy",Path=files[0],FileHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files[0]))),Width=copy.Width,Height=copy.Height,BitmapWidth=bitmap?.Width,BitmapHeight=bitmap?.Height};
   File.WriteAllText(Path.Combine(folder,"received.json"),JsonSerializer.Serialize(record,new JsonSerializerOptions{WriteIndented=true}));
   label.Text=$"已收到副本 · {copy.Width} × {copy.Height} · 原图应仍在晾衣绳上";e.Effect=DragDropEffects.Copy;
  };
  Application.Run(form);
 }
}