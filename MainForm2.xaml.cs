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
using System.Windows.Data;
using System.Net;
using System.IO;
using System.Xml;
using Esri.FileGDB;
using System.Device.Location;


namespace ViewerOne
{
  public class TextInputToVisibilityConverter : IMultiValueConverter
  {
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      // Always test MultiValueConverter inputs for non-null
      // (to avoid crash bugs for views in the designer)
      if (values[0] is bool && values[1] is bool)
      {
        bool hasText = !(bool)values[0];
        bool hasFocus = (bool)values[1];

        if (hasFocus || hasText)
          return Visibility.Collapsed;
      }

      return Visibility.Visible;
    }


    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }


  /// <summary>
  /// Interaction logic for MainForm2.xaml
  /// </summary>
  public partial class MainForm2 : Window
  {
    //GeodatabaseFeatureServiceTable gdbFeatureServiceTable = null;

    FeatureLayer fl = null;
    private bool _isOnline = true;
    LocatorTask _locatorTask;
    GraphicsLayer incidentsGraphicsLayer;
    GraphicsLayer storedIncidentsGraphicsLayer;

    SpatialReference wgs84 = new SpatialReference(4326);
    SpatialReference webMercator = new SpatialReference(102100);
    long recNo = -1;

    public MainForm2()
    {
      try
      {
        InitializeComponent();

        mapTipPoint.Visibility = System.Windows.Visibility.Collapsed;
        Task t2 = setIncidentSymbol();
        Task t = setStoredIncidentSymbol();
        clearUI();
      }
      catch(Exception ex)
      {
        MessageBox.Show("init: " + ex.Message);

      }
    }

    private void txtSearch_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter)
      {
        var task = doFind();
      }
      else if (txtSearch.Text.Length == 0)
      {

//        this.incidentsGraphicsLayer.Graphics.Clear();

      }
    }

    private async void mapView_MouseMove(object sender, MouseEventArgs e)
    {
      try
      {
        System.Windows.Point screenPoint = e.GetPosition(mapView);

        MapPoint mapPoint = mapView.ScreenToLocation(screenPoint);
        Esri.ArcGISRuntime.Geometry.MapPoint ll = Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(mapPoint, new SpatialReference(4236)) as Esri.ArcGISRuntime.Geometry.MapPoint;
        string sCoords = string.Format("Latitude: {1}, Longitude: {0}",
                            Math.Round(ll.X, 2), Math.Round(ll.Y, 2));
        mapCoords.Text = sCoords;

        Graphic x = await storedIncidentsGraphicsLayer.HitTestAsync(mapView, screenPoint);
        if (x!=null)
        {
          this.mapView.Cursor = Cursors.Hand;

          var feature = x;

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

    void mapView_MouseUp(object sender, MouseButtonEventArgs e)
    {

     

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

    private async Task LoadEditableData()
    {

    }

    private async Task LoadOperationalData()
    {

      string sPath = @"Data\localgovernment.geodatabase";
      Esri.ArcGISRuntime.Data.Geodatabase gdb = await Esri.ArcGISRuntime.Data.Geodatabase.OpenAsync(sPath);

      Esri.ArcGISRuntime.Geometry.Envelope extent = null;// new Esri.ArcGISRuntime.Geometry.Envelope();
     //SBTEST 10.2.4 Esri.ArcGISRuntime.Geometry.Envelope extent = new Esri.ArcGISRuntime.Geometry.Envelope();
     
      foreach (var table in gdb.FeatureTables)
      {

        var flayer = new FeatureLayer()
        {
          ID = table.Name,
          DisplayName = table.Name,

          FeatureTable = table
        };


        if (table.ServiceInfo.Extent != null)
        {
          if (extent == null)
            extent = table.ServiceInfo.Extent;
          else
            extent = extent.Union(table.ServiceInfo.Extent);
        }


        mapView.Map.Layers.Add(flayer);
      }

      await mapView.SetViewAsync(extent.Expand(1.10));

      

    }

    private void ZoomToDefault()
    {

      //40,43,-89,-86
      //SBTEST 10.2.4 Esri.ArcGISRuntime.Geometry.Envelope env = new Esri.ArcGISRuntime.Geometry.Envelope(-89, 40, -86, 43);
      Esri.ArcGISRuntime.Geometry.Envelope env = new Esri.ArcGISRuntime.Geometry.Envelope(-89, 40, -86, 43, new SpatialReference(4326));
      //SBTEST 10.2.4 env.SpatialReference = new SpatialReference(4326);

      Esri.ArcGISRuntime.Geometry.Envelope env2= Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(env, new SpatialReference(102100)) as Esri.ArcGISRuntime.Geometry.Envelope;

      mapView.SetView(env2);

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
      catch (Exception ex)
      {


      }
    }

    static void GetLocationProperty()
    {
      GeoCoordinateWatcher watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);

      // Do not suppress prompt, and wait 1000 milliseconds to start.
      //watcher.TryStart(false, TimeSpan.FromMilliseconds(5000));
      watcher.StatusChanged += watcher_StatusChanged;
      watcher.Start(false);
      
      GeoCoordinate coord = watcher.Position.Location;

      if (coord.IsUnknown != true)
      {
        Console.WriteLine("Lat: {0}, Long: {1}",
            coord.Latitude,
            coord.Longitude);
        MessageBox.Show(string.Format("Current Location is Lat: {0}, Long: {1}",
            coord.Latitude,
            coord.Longitude));
      }
      else
      {
        Console.WriteLine("Unknown latitude and longitude.");
        MessageBox.Show("Unknown latitutde and longitude.");

      }
    }

    static void watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
    {

    }
    private void mapView_Initialized(object sender, EventArgs e)
    {

      try
      {

        LoadBasemapOnline();

        // var task2 = LoadEditableData();

        var task3 = LoadOperationalData();

        SetupLocator();

        incidentsGraphicsLayer = new GraphicsLayer();
        this.mapView.Map.Layers.Add(incidentsGraphicsLayer);

        storedIncidentsGraphicsLayer = new GraphicsLayer();
        this.mapView.Map.Layers.Add(storedIncidentsGraphicsLayer);

        this.mapView.MouseUp += mapView_MouseUp;

        ZoomToDefault();

        refreshStoredIncidents();
      }
      catch(Exception ex)
      {
        MessageBox.Show("map init: " + ex.Message);

      }
    }

    private void refreshStoredIncidents()
    {
      try
      {
        if (myStoredIncidentSymbol == null)
        {
          Task t = setStoredIncidentSymbol();
        }

        //remove pending graphics
        incidentsGraphicsLayer.Graphics.Clear();

        //
        // Open the geodatabase.
        Esri.FileGDB.Geodatabase geodatabase = Esri.FileGDB.Geodatabase.Open(@"Data/MyIncidents.gdb");

        // Open the Incidents table.
        Table table = geodatabase.OpenTable("\\Incidents");

        Graphic gIncident = null;
        foreach (Row avRow in table.Search("*", "", RowInstance.Recycle))
        {
          gIncident = new Graphic();

          ShapeBuffer sb = avRow.GetGeometry();
          PointShapeBuffer geometry = avRow.GetGeometry();
          Esri.FileGDB.Point point = geometry.point;
          double x = avRow.GetDouble("LON");
          double y = avRow.GetDouble("LAT");
          string notes = avRow.GetString("NOTES");
          gIncident.Attributes.Add(new KeyValuePair<string, object>("NOTES", notes));
          gIncident.Attributes.Add(new KeyValuePair<string, object>("LAT", y));
          gIncident.Attributes.Add(new KeyValuePair<string, object>("LON", x));

          MapPoint ll = new MapPoint(x, y, new SpatialReference(4326));
          MapPoint mapPoint = Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(ll, new SpatialReference(102100)) as MapPoint;

          gIncident.Geometry = mapPoint;
          gIncident.Symbol = myStoredIncidentSymbol;

          storedIncidentsGraphicsLayer.Graphics.Add(gIncident);



        }

        table.Close();
        geodatabase.Close();
      }
      catch(Exception ex)
      {
        MessageBox.Show("refreshStoredIncidents: " + ex.Message);

      }

    }

    private void FindButton_Click(object sender, RoutedEventArgs e)
    {
      var task = doFind();
    }

    private PictureMarkerSymbol myIncidentSymbol = null;
    private PictureMarkerSymbol myStoredIncidentSymbol = null;

    private async Task setStoredIncidentSymbol()
    {

      myStoredIncidentSymbol = new PictureMarkerSymbol();
      await myStoredIncidentSymbol.SetSourceAsync(new Uri("pack://application:,,,/IncidentCollectionViewerDemo;component/Images/blue.png"));

      myStoredIncidentSymbol.Height = 18;
      myStoredIncidentSymbol.Width = 18;

    }

    private async Task setIncidentSymbol()
    {

      myIncidentSymbol = new PictureMarkerSymbol() ;
      await myIncidentSymbol.SetSourceAsync(new Uri("pack://application:,,,/IncidentCollectionViewerDemo;component/Images/purple.png"));

      myIncidentSymbol.Height = 18;
      myIncidentSymbol.Width = 18;

    }

    private void addIncidentPoint(MapPoint ll)
    {


      MapPoint mapPoint = Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(ll, new SpatialReference(102100)) as MapPoint;

      Graphic graphic = new Graphic() { Geometry = mapPoint };
      ensureAttributes(graphic);

      graphic.Symbol = myIncidentSymbol;
      incidentsGraphicsLayer.Graphics.Clear();
      incidentsGraphicsLayer.Graphics.Add(graphic);
      mapTipPoint.Visibility = System.Windows.Visibility.Visible;

      mapView.SetView(mapPoint, 10000);

    }

    private async Task doFind()
    {

      string sAddress = txtSearch.Text;
      LocatorServiceInfo locatorServiceInfo = await _locatorTask.GetInfoAsync();
      Dictionary<string, string> inputAddress = new Dictionary<string, string>() { { locatorServiceInfo.SingleLineAddressField.FieldName, sAddress } };
      IList<LocatorGeocodeResult> _candidateResults = await _locatorTask.GeocodeAsync(inputAddress, new List<string> { "Match_addr" }, this.mapView.SpatialReference, CancellationToken.None);

      LocatorGeocodeResult result = _candidateResults.FirstOrDefault();
      if (result != null && result.Extent != null)
      {
        mapView.SetView(result.Extent);
        mapView.ZoomToScaleAsync(10000, new TimeSpan(0, 0, 1));

        //SBTEST 10.2.4 MapPoint mapPoint =  result.Location;
        //SBTEST 10.2.4 mapPoint.SpatialReference = this.mapView.SpatialReference;
        MapPoint mapPoint = new MapPoint(  result.Location.X,result.Location.Y,this.mapView.SpatialReference);
     
        MapPoint ll = Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(mapPoint, new SpatialReference(4326)) as MapPoint;
        m_currentLL = ll;

        Graphic graphic = new Graphic() { Geometry = result.Location };
        graphic.Attributes.Add(new KeyValuePair<string, object>("LAT", ll.Y));
        graphic.Attributes.Add(new KeyValuePair<string, object>("LON", ll.X));
        graphic.Attributes.Add(new KeyValuePair<string, object>("INCIDENTDATE", this.dtPicker.Text));
        graphic.Attributes.Add(new KeyValuePair<string, object>("OFFICER", this.txtName.Text));
        graphic.Attributes.Add(new KeyValuePair<string, object>("INCIDENTTYPE", this.cmbType.Text));
        graphic.Attributes.Add(new KeyValuePair<string, object>("NOTES", this.txtNotes.Text));


        graphic.Symbol = myIncidentSymbol;
        incidentsGraphicsLayer.Graphics.Clear();
        incidentsGraphicsLayer.Graphics.Add(graphic);
        mapTipPoint.Visibility = System.Windows.Visibility.Visible;



      }

    }

    private void addLatLon_Click(object sender, RoutedEventArgs e)
    {

      double lat = Convert.ToDouble(txtLat.Text);
      double lon = Convert.ToDouble(txtLon.Text);

      MapPoint ll = new MapPoint(lon, lat, new SpatialReference(4326));
      m_currentLL = ll;
      addIncidentPoint(ll);
      
    }

    private void btnGPS_Click(object sender, RoutedEventArgs e)
    {

      GetLocationProperty();
      return;


        //create a request to geoiptool.com
        var request = WebRequest.Create(new Uri("http://geoiptool.com/data.php")) as HttpWebRequest;


        if (request != null)
        {
          //set the request user agent
          request.UserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0; SLCC1; .NET CLR 2.0.50727)";

          //get the response
          using (var webResponse = (request.GetResponse() as HttpWebResponse))
            if (webResponse != null)
              using (var reader = new StreamReader(webResponse.GetResponseStream()))
              {
                //get the XML document
                var doc = new XmlDocument();
                doc.Load(reader);

                //now we parse the XML document
                var nodes = doc.GetElementsByTagName("marker");

                //make sure we have nodes before looping
                //if (nodes.Count > 0)
                //{
                //grab the first response
                var marker = nodes[0] as XmlElement;

                var latitude = marker.GetAttribute("lat");
                var longitude = marker.GetAttribute("lng");

              }

        }
    }

    private void mapView_MapViewTapped(object sender, MapViewInputEventArgs e)
    {
      //add temp point
      MapPoint mapPoint = e.Location;
      MapPoint ll = Esri.ArcGISRuntime.Geometry.GeometryEngine.Project(mapPoint, new SpatialReference(4326)) as MapPoint;
      m_currentLL=ll;
      
      Graphic graphic = new Graphic() { Geometry = mapPoint};
      ensureAttributes(graphic);

      graphic.Symbol = myIncidentSymbol;
      incidentsGraphicsLayer.Graphics.Clear();
      incidentsGraphicsLayer.Graphics.Add(graphic);


      System.Windows.Point screenPoint = e.Position;

      //maptipPointTransform.X = screenPoint.X + 4;
      //maptipPointTransform.Y = screenPoint.Y - mapTipPoint.ActualHeight;
      mapTipPoint.Visibility = System.Windows.Visibility.Visible;



    }

    private void btnMapTipClose_Click(object sender, RoutedEventArgs e)
    {
      mapTipPoint.Visibility = System.Windows.Visibility.Collapsed;
    }
    private void ensureAttributes(Graphic graphic)
    {
      graphic.Attributes.Clear();


      if (m_currentLL != null)
      {
        graphic.Attributes.Add(new KeyValuePair<string, object>("LAT", m_currentLL.Y));
        graphic.Attributes.Add(new KeyValuePair<string, object>("LON", m_currentLL.X));
      }

      if (this.dtPicker.Text!=null)graphic.Attributes.Add(new KeyValuePair<string, object>("INCIDENTDATE", this.dtPicker.Text));
      if(this.txtName.Text!=null)graphic.Attributes.Add(new KeyValuePair<string, object>("OFFICER", this.txtName.Text));
      if(this.cmbType.Text!=null)graphic.Attributes.Add(new KeyValuePair<string, object>("INCIDENTTYPE", this.cmbType.Text));
      if (this.txtNotes.Text!=null) graphic.Attributes.Add(new KeyValuePair<string, object>("NOTES", this.txtNotes.Text));


    }

    MapPoint m_currentLL = null;

    private void btnSubmit_Click(object sender, RoutedEventArgs e)
    {

      if (this.incidentsGraphicsLayer.Graphics == null || this.incidentsGraphicsLayer.Graphics.Count < 1)
      {
        MessageBox.Show("Please ensure you have entered a valid location and information for the incident.");
        return;

      }

      // Open the geodatabase.
      Esri.FileGDB.Geodatabase geodatabase = Esri.FileGDB.Geodatabase.Open(@"Data/MyIncidents.gdb");

      // Open the Incidents table.
      Table table = geodatabase.OpenTable("\\Incidents");

      Graphic g = this.incidentsGraphicsLayer.Graphics[0];
      ensureAttributes(g);

      // Create a new feature 
      Row newRow = table.CreateRowObject();
      newRow.SetDate("INCIDENTDATE", Convert.ToDateTime(g.Attributes["INCIDENTDATE"]));
      newRow.SetString("OFFICER", Convert.ToString(g.Attributes["OFFICER"]));
      newRow.SetDouble("LAT", Convert.ToDouble( g.Attributes["LAT"]));
      newRow.SetDouble("LON", Convert.ToDouble(g.Attributes["LON"]));
      newRow.SetString("INCIDENTTYPE", Convert.ToString(g.Attributes["INCIDENTTYPE"]));
      newRow.SetString("NOTES", Convert.ToString(g.Attributes["NOTES"]));


      // Create and assign a point geometry.
      PointShapeBuffer rowGeom = new PointShapeBuffer();
      rowGeom.Setup(ShapeType.Point);

      double x=Convert.ToDouble(g.Attributes["LON"]);
      double y = Convert.ToDouble(g.Attributes["LAT"]);

      Esri.FileGDB.Point point = new Esri.FileGDB.Point(x, y);
      rowGeom.point = point;
      newRow.SetGeometry(rowGeom);

      table.Insert(newRow);
      table.Close();
      geodatabase.Close();
      MessageBox.Show("Submitted.");

      clearUI();
      refreshStoredIncidents();
    }

    private void clearUI()
    {
      mapTipPoint.Visibility = System.Windows.Visibility.Collapsed;
      this.txtLat.Text = "";
      this.txtLon.Text = "";
      this.txtName.Text = "";
      this.txtNotes.Text = "";
      this.txtSearch.Text = "";
      this.cmbType.SelectedIndex = 0;
      this.dtPicker.SelectedDate = DateTime.Today;
      m_currentLL = null;

    }

    private void mapView_LayerLoaded(object sender, LayerLoadedEventArgs e)
    {
      try
      {
        ZoomToDefault();
      }
      catch(Exception ex)
      {
        MessageBox.Show("layer loaded: " + ex.Message);
      }
    }

    private void btnZoomIn_Click(object sender, RoutedEventArgs e)
    {
      mapView.ZoomAsync(1.4);
    }

    private void btnZoomOut_Click(object sender, RoutedEventArgs e)
    {
      mapView.ZoomAsync(.6);
    }

    private void btnZoomHome_Click(object sender, RoutedEventArgs e)
    {
      ZoomToDefault();
    }

    private void chkOnline_Checked(object sender, RoutedEventArgs e)
    {
      _isOnline = true;

      setOnlineStatus();



    }

    private void setOnlineStatus()
    {

      if (_isOnline)
      {
        string sURL = "http://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer";
        LoadBasemapOnline(sURL);
      }
      else
      {
        LoadBasemapPackage();
      }

      SetupLocator();
    }
    private void chkOnline_Unchecked(object sender, RoutedEventArgs e)
    {
      _isOnline = false;
      setOnlineStatus();
    }

  }


}
