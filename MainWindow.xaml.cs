using Esri.ArcGISRuntime.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Esri.ArcGISRuntime.Data;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.LocalServices;
using System.Collections.Generic;
using Esri.ArcGISRuntime.Tasks.Edit;
using System.Windows.Input;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using System.Threading;
using Esri.ArcGISRuntime.Symbology;
using System.Windows.Media;
using System.Windows.Controls.Ribbon;
using System.Windows.Controls;

namespace ViewerOne
{
  public partial class MainWindow : Window
  {
    GeodatabaseFeatureServiceTable gdbFeatureServiceTable = null;
    FeatureLayer fl = null;
    private bool _isOnline = true;
    LocatorTask _locatorTask;
    GraphicsLayer addressesGraphicsLayer;
    SpatialReference wgs84 = new SpatialReference(4326);
    SpatialReference webMercator = new SpatialReference(102100);
    long recNo = -1;
    
    
    public MainWindow()
    {
      InitializeComponent();
      
    }


    private void RibbonGallery_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      RibbonGalleryItem r = e.NewValue as RibbonGalleryItem;
      string s = r.Name;
      string sURL =r.Tag.ToString();

      rsb.Label = s;
      rsb.SmallImageSource = (r.Content as Image).Source;// r.Tag.ToString();
      rsb.LargeImageSource = (r.Content as Image).Source;
      

      //change basemap
      if (_isOnline)
        LoadBasemapOnline(sURL);

    }
    private async Task getInputPoint()
    {

      this.mapView.Cursor = Cursors.Hand;
      // get a point from the user
      var mapPoint = await this.mapView.Editor.RequestPointAsync();
      this.mapView.Cursor = Cursors.Arrow;

      var newFeature = new Esri.ArcGISRuntime.Data.GeodatabaseFeature(this.gdbFeatureServiceTable.Schema);
      newFeature.Geometry = mapPoint;
      newFeature.Attributes["IncidentName"] = "Default";
      newFeature.Attributes["Comments"] = "Default";
      if (this.txtName.Text != null && this.txtName.Text.Length > 0) newFeature.Attributes["IncidentName"] = this.txtName.Text;

      MapPoint ll = Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(mapPoint, new SpatialReference(4326)) as MapPoint;

      try
      {
        recNo = await this.gdbFeatureServiceTable.AddAsync(newFeature);
      }
      catch (Exception ex)
      {
        string s = ex.Message;

      }
    }

    private void DoSubmit(object sender, RoutedEventArgs e)
    {
      var t = SaveEdits();
      MessageBox.Show("Submitted.");

    }

    private async void mapView_MouseMove(object sender, MouseEventArgs e)
    {
      try
      {
        System.Windows.Point screenPoint = e.GetPosition(mapView);
        var rows = await fl.HitTestAsync(mapView, screenPoint);
        if (rows != null && rows.Length > 0)
        {
          this.mapView.Cursor = Cursors.Hand;
          var features = await fl.FeatureTable.QueryAsync(rows);
          var feature = features.FirstOrDefault();

          maptipTransform.X = screenPoint.X + 4;
          maptipTransform.Y = screenPoint.Y - mapTip.ActualHeight;
          mapTip.DataContext = feature;
          mapTip.Visibility = System.Windows.Visibility.Visible;
          this.mapView.Cursor = Cursors.Arrow;
        }
        else
          mapTip.Visibility = System.Windows.Visibility.Hidden;
      }
      catch
      {
        mapTip.Visibility = System.Windows.Visibility.Hidden;
      }
    }

    private async Task SaveEdits()
    {
      try
      {
        string sName = txtName.Text;//get name

        //commit and then update attributes
        var idList = new List<long> { this.recNo };
        FeatureEditResult results = null;
        
        results = await gdbFeatureServiceTable.ApplyEditsAsync();
        foreach (FeatureEditResultItem result in results.AddResults)
        {
          //check the feature.
          System.Diagnostics.Debug.WriteLine(result.Success.ToString());
        }

        var myID = results.AddResults[0].ObjectID;
        idList = new List<long> { myID };

        // query the table for the features with the specified IDs
        var updateFeatures = await this.gdbFeatureServiceTable.QueryAsync(idList);

        // get the first GeodatabaseFeature (should be one or zero)
        var feature = updateFeatures.FirstOrDefault();
        feature.Attributes["IncidentName"] = sName;

        await gdbFeatureServiceTable.UpdateAsync(feature as GeodatabaseFeature);

        results = await gdbFeatureServiceTable.ApplyEditsAsync();

        foreach (FeatureEditResultItem result in results.UpdateResults)
        {

          System.Diagnostics.Debug.WriteLine(result.Success.ToString());
        }
      }
      catch (Exception ex)
      {

        MessageBox.Show("Error: " + ex.Message);

      }
    }

    private async Task LoadEditableData()
    {

      try
      {
        string sPath = @"Data\incidents.mpk";

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

          if ((fl.FeatureTable).ServiceInfo.Extent != null)
          {
            await mapView.SetViewAsync((fl.FeatureTable).ServiceInfo.Extent.Expand(1.10));
            pbar.Visibility = System.Windows.Visibility.Hidden;

            rg.SelectedItem = rgc.Items[0];
          }
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message);

      }
    }

    private async Task LoadOperationalData()
    {

      string sPath = @"Data\localgovernment.geodatabase";
      Geodatabase gdb = await Esri.ArcGISRuntime.Data.Geodatabase.OpenAsync(sPath);

      Envelope extent = new Envelope();
      foreach (var table in gdb.FeatureTables)
      {

        var flayer = new FeatureLayer()
        {
          ID = table.Name,
          DisplayName = table.Name,
            
          FeatureTable = table
        };


        if (table.Extent != null)
        {
          if (extent == null)
            extent = table.Extent;
          else
            extent = extent.Union(table.Extent);
        }


        mapView.Map.Layers.Add(flayer);
      }

      //await mapView.SetViewAsync(extent.Expand(1.10));


    }

    private void AddLocation_Click(object sender, RoutedEventArgs e)
    {
      var task2 = getInputPoint();


    }

    private void LoadBasemapPackage()
    {
      if (mapView == null) return;
      var sPath = @"Data\Basemap.tpk";
      var b = mapView.Map.Layers["BASEMAP"];
      if (b != null) mapView.Map.Layers.Remove(b);

      var basemapLayer = new ArcGISLocalTiledLayer(sPath);
      basemapLayer.ID = "BASEMAP";
      mapView.Map.Layers.Insert(0, basemapLayer);

    }

    private void mapView_Initialized(object sender, EventArgs e)
    {

      LoadBasemapOnline();

      var task2 =  LoadEditableData();
      
      var task3 = LoadOperationalData();
      SetupLocator();

      addressesGraphicsLayer = new GraphicsLayer();     
      this.mapView.Map.Layers.Add(addressesGraphicsLayer);


      
    }


    private void LoadBasemapOnline(string sURL)
    {

      if (mapView == null) return;
      var b = mapView.Map.Layers["BASEMAP"];
      if (b != null) mapView.Map.Layers.Remove(b);


      var t = new ArcGISTiledMapServiceLayer(new Uri(sURL));
      t.ID = "BASEMAP";
      mapView.Map.Layers.Insert(0, t);

    }

    private void LoadBasemapOnline()
    {

      if (mapView == null) return;
      var b = mapView.Map.Layers["BASEMAP"];
      if (b != null) mapView.Map.Layers.Remove(b);


      var t = new ArcGISTiledMapServiceLayer(new Uri("http://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer"));
      t.ID = "BASEMAP";
      mapView.Map.Layers.Insert(0, t);

    }

    //private void RadioButton_Checked(object sender, RoutedEventArgs e)
    //{
    //  //offline
    //  _isOnline = false;
    //  LoadBasemapPackage();
    //  SetupLocator();
    //}

    //private void RadioButton_Checked_1(object sender, RoutedEventArgs e)
    //{
    //  //online
    //  _isOnline = true;
    //  LoadBasemapOnline();
    //  SetupLocator();
    //}

    private async Task SetupLocator()
    {
      try
      {
        if (!_isOnline)
        {

          _locatorTask = new LocalLocatorTask(@"Data\StreetName.loc");
        }
        else
        {
          _locatorTask = new OnlineLocatorTask(new Uri("http://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer"), string.Empty);
        }
      }
      catch(Exception ex)
      {


      }
    }

    private void txtSearch_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter)
      {
         var task =doFind();
      }
      else if (txtSearch.Text.Length == 0)
      {

        this.addressesGraphicsLayer.Graphics.Clear();

      }
    }

    private void FindButton_Click(object sender, RoutedEventArgs e)
    {
      var task= doFind();
    }

    private async Task doFind()
    {

      string sAddress = txtSearch.Text;
      LocatorServiceInfo locatorServiceInfo = await _locatorTask.GetInfoAsync();
      Dictionary<string, string> inputAddress = new Dictionary<string, string>() { { locatorServiceInfo.SingleLineAddressField.FieldName, sAddress } };
      IList<LocatorGeocodeResult> _candidateResults = await _locatorTask.GeocodeAsync(inputAddress, new List<string> { "Match_addr" }, this.mapView.SpatialReference, CancellationToken.None);

      LocatorGeocodeResult result = _candidateResults.FirstOrDefault();
      if(result!=null && result.Extent!=null)
      { 
        mapView.SetView(result.Extent);

        Graphic graphic = new Graphic() { Geometry = result.Location };
        SimpleMarkerSymbol sms = new SimpleMarkerSymbol();
        sms.Style = SimpleMarkerStyle.Diamond;
        sms.Color = Colors.Blue;
        sms.Size = 10;
        graphic.Symbol = sms;
        addressesGraphicsLayer.Graphics.Add(graphic);

      }

    }

    private void RibbonGallery_SelectionChanged_1(object sender, RoutedPropertyChangedEventArgs<object> e)
    {

      string sOnline=(e.NewValue as System.Windows.Controls.Ribbon.RibbonGalleryItem).Tag.ToString();
      
      if (sOnline.ToUpper() == "ONLINE") { _isOnline = true; } else { _isOnline = false; };
      rmbConn.Label = (e.NewValue as System.Windows.Controls.Ribbon.RibbonGalleryItem).Content.ToString();

      if (_isOnline)
      {
        RibbonGalleryItem r = rg.SelectedItem as RibbonGalleryItem;
        string s = r.Name;
        string sURL = r.Tag.ToString();

        LoadBasemapOnline(sURL);
      }
      else
      {
        LoadBasemapPackage();
      }
      SetupLocator();

    }
  }
}
