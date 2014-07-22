IncidentCollectionViewerDemo
============================

ArcGIS Runtime SDK for .NET Offline Editing Example

ArcGIS Runtime SDK for .NET 10.2.3 can use the new runtime geodatabase for offline editing but it needs as its parent a feature service.  This sample uses a service-less methodology by starting with a map package exported from ArcGIS for the editable "LocalFeatureService" (Incidents).

Update:
This approach does not use the Runtime LOCALSERVER option to wrap a map package from ArcMap and just uses the map to draw temporary graphics.  I cleaned up the UI also.  But at the same time I added another little piece for fun using what the File Geodatabase API (unrelated to the Runtime) to persist and load the features in the app.  You could do the same thing using custom XML or CSV for sure but this is kind of nice because you can access the file geodatabase independently of the app.

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/newscreen.png)

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image002.jpg)
 


       string sPath = @"..\..\Data\incidents.mpk";

        // Create a new local feature service instance and supply an ArcGIS Map Package path as string.
        var localFeatureService = new LocalFeatureService(sPath);// LocalFeatureService.GetServiceAsync(sPath);
        
        await localFeatureService.StartAsync();
        if (localFeatureService != null)
        {


          Esri.ArcGISRuntime.Tasks.Query.OutFields oof = new Esri.ArcGISRuntime.Tasks.Query.OutFields();
          oof.Add("OBJECTID");
          oof.Add("Comments");
          oof.Add("IncidentName");

          gdbFeatureServiceTable = new GeodatabaseFeatureServiceTable()
          {
            ServiceUri = localFeatureService.UrlFeatureService + "/0",
            OutFields = oof
          };

          await gdbFeatureServiceTable.InitializeAsync();

          fl = new FeatureLayer(gdbFeatureServiceTable) { ID = "featureLayer" };

          mapView.Map.Layers.Add(fl);


A read-only "Operational Layer" (counties) is added by using the runtime geodatabase option generated from the File | Share As | Runtime Content command.  

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image003.jpg.png)



An offline basemap was generated by using a sample code (https://github.com/Esri/arcgis-runtime-samples-dotnet/blob/master/src/Desktop/ArcGISRuntimeSDKDotNet_DesktopSamples/Samples/Offline/ExportTileCache.xaml.cs).

http://doc.arcgis.com/en/collector/android/create-maps/offline-map-prep.htm


An offline locator is also used which is generated by adding it to the map before generating the runtime content and setting the option to include it in the output.


![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image006.jpg)

 

The result is a directory with the offline basemap, the map package for incidents, the runtime geodatabase with the operational layer (localgovernment.geodatabase), and the offline locator (streetname.loc).

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image004.jpg)

The app loads with an online basemap, the local operational layer, and the local editable feature service.
 

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image008.jpg)

 
Clicking the Offline option switches to use the local tile cache basemap.

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image009.jpg.png)

When offline, the offline locator is used.  The demo is based on Naperville.  Eagle St is a valid street to search for.


![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/napervilleaddress.png)
 
The Add Location tool adds a point to the local editable feature service.  

      var mapPoint = await this.mapView.Editor.RequestPointAsync();
      this.mapView.Cursor = Cursors.Arrow;

      var newFeature = new Esri.ArcGISRuntime.Data.GeodatabaseFeature(this.gdbFeatureServiceTable.Schema);
      newFeature.Geometry = mapPoint;
      newFeature.Attributes["IncidentName"] = "Default";
      newFeature.Attributes["Comments"] = "Default";
      if (this.txtName.Text != null && this.txtName.Text.Length > 0) newFeature.Attributes["IncidentName"] = this.txtName.Text;

      try
      {
        recNo = await this.gdbFeatureServiceTable.AddAsync(newFeature);
      }


The submit button applies the edit to the local feature service.

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image010.jpg)

 
 
When the local feature service was created, the map package of incidents was unpacked to a windows directory as a file geodatabase.  The default location, which can be changed programmatically is:
C:\Users\<username>\AppData\Local\ArcGISRuntime\Documents\ArcGIS\Packages 
 

![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image012.jpg)

 
After the edits have been applied, they are persisted in the underlying file geodatabase and can be reviewed in ArcGIS desktop, or custom code could export them to alternative formats.
 
![](https://raw.githubusercontent.com/sam-berg/IncidentCollectionViewerDemo/master/Images/clip_image013.png)






