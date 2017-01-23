using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace toll_booth_sim {
	public partial class MainWindow : Window {
		private DispatcherTimer _timer = new DispatcherTimer();
		private Stopwatch _stw = new Stopwatch();
		private double _last = 0.0;

		public MainWindow() {
			InitializeComponent();

			_timer.Interval = TimeSpan.FromMilliseconds(1.0);
			_timer.Tick += (sender, e) => {
				double cd = _stw.Elapsed.TotalSeconds;
				if (cd - _last > 1.0) {
					_last = cd - 1.0;
				}
				while (cd > _last) {
					main_env.update_noredraw(1.0 / 30.0);
					_last += 1.0 / (30.0 * speed.Value);
				}
				time_label.Content = String.Format("t = {0:F2}", main_env.time);
				main_env.InvalidateVisual();
			};

			main_env.detailed_info_logger = new StreamWriter("carpos.txt");
			main_env.event_logger = new StreamWriter("events.txt");
		}

		private void add_node_click(object sender, RoutedEventArgs e) {
			main_env.nodes.Add(new road_node() { id = main_env.node_id_counter++ });
			main_env.InvalidateVisual();
		}

		private void delete_object_Click(object sender, RoutedEventArgs e) {
			if (main_env.focused_object is road_node) {
				main_env.delete_node((road_node)main_env.focused_object);
			} else if (main_env.focused_object is lane) {
				main_env.delete_lane((lane)main_env.focused_object);
			}
		}

		private void add_lane_checked(object sender, RoutedEventArgs e) {
			main_env.creating_lane = true;
		}
		private void add_lane_unchecked(object sender, RoutedEventArgs e) {
			main_env.creating_lane = false;
		}

		private void change_node_type(node_type type) {
			road_node n = main_env.focused_object as road_node;
			if (n != null) {
				n.type = type;
				main_env.InvalidateVisual();
			}
		}

		private void mid_selected(object sender, RoutedEventArgs e) {
			change_node_type(node_type.intermediate);
		}
		private void start_selected(object sender, RoutedEventArgs e) {
			change_node_type(node_type.booth);
		}
		private void end_selected(object sender, RoutedEventArgs e) {
			change_node_type(node_type.exit);
		}
		private void spawntime_min_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			road_node n = main_env.focused_object as road_node;
			if (n != null) {
				n.spawn_interval_min = spawntime_min.Value;
			}
		}
		private void spawntime_max_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			road_node n = main_env.focused_object as road_node;
			if (n != null) {
				n.spawn_interval_max = spawntime_max.Value;
			}
		}

		private void main_env_focus_change() {
			node_settings_grid.Visibility = Visibility.Hidden;
			delete_object.IsEnabled = main_env.focused_object != null;
			if (main_env.focused_object is road_node) {
				road_node focus = (road_node)main_env.focused_object;
				node_settings_grid.Visibility = Visibility.Visible;
				switch (focus.type) {
					case node_type.intermediate:
						type_mid.IsSelected = true;
						break;
					case node_type.booth:
						type_start.IsSelected = true;
						break;
					case node_type.exit:
						type_end.IsSelected = true;
						break;
				}
				spawntime_min.Value = focus.spawn_interval_min;
				spawntime_max.Value = focus.spawn_interval_max;
			}
		}

		private void load(StreamReader reader) {
			main_env.reset();
			main_env.nodes.Clear();
			main_env.lanes.Clear();
			main_env.node_id_counter = 0;
			Dictionary<int, road_node> dict = new Dictionary<int, road_node>();
			int nnodes = int.Parse(reader.ReadLine());
			for (int i = 0; i < nnodes; ++i) {
				string[] strs = reader.ReadLine().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				road_node nd = new road_node();
				nd.position.X = double.Parse(strs[0]);
				nd.position.Y = double.Parse(strs[1]);
				nd.type = (node_type)int.Parse(strs[2]);
				nd.id = int.Parse(strs[3]);
				//nd.spawn_interval_min = double.Parse(strs[4]);
				//nd.spawn_interval_max = double.Parse(strs[5]);
				main_env.node_id_counter = Math.Max(nd.id + 1, main_env.node_id_counter);
				main_env.nodes.Add(nd);
				dict[nd.id] = nd;
			}
			int nlanes = int.Parse(reader.ReadLine());
			for (int i = 0; i < nlanes; ++i) {
				string[] strs = reader.ReadLine().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				lane l = new lane();
				l.from = dict[int.Parse(strs[0])];
				l.to = dict[int.Parse(strs[1])];
				l.from.out_lanes.Add(l);
				l.to.in_lanes.Add(l);
				l.ctrl_from.X = double.Parse(strs[2]);
				l.ctrl_from.Y = double.Parse(strs[3]);
				l.ctrl_to.X = double.Parse(strs[4]);
				l.ctrl_to.Y = double.Parse(strs[5]);
				l.make_cache();
				main_env.lanes.Add(l);
			}
			main_env.InvalidateVisual();
		}
		private void save(StreamWriter writer) {
			writer.WriteLine(main_env.nodes.Count);
			foreach (road_node n in main_env.nodes) {
				//writer.WriteLine(
				//	"{0} {1} {2} {3} {4} {5}",
				//	n.position.X, n.position.Y,
				//	(int)n.type, n.id,
				//	n.spawn_interval_min, n.spawn_interval_max
				//);
				writer.WriteLine(
					"{0} {1} {2} {3}",
					n.position.X, n.position.Y,
					(int)n.type, n.id
				);
			}
			writer.WriteLine(main_env.lanes.Count);
			foreach (lane l in main_env.lanes) {
				writer.WriteLine(
					"{0} {1} {2} {3} {4} {5}",
					l.from.id, l.to.id,
					l.ctrl_from.X, l.ctrl_from.Y,
					l.ctrl_to.X, l.ctrl_to.Y
				);
			}
		}
		private void save_click(object sender, RoutedEventArgs e) {
			if (File.Exists(save_file_name.Text)) {
				if (MessageBox.Show(
					String.Format("File {0} already exists. Proceed?", save_file_name.Text),
					"Warning",
					MessageBoxButton.YesNo
				) == MessageBoxResult.No) {
					return;
				}
			}
			using (StreamWriter writer = new StreamWriter(save_file_name.Text)) {
				save(writer);
			}
		}
		private void load_click(object sender, RoutedEventArgs e) {
			using (StreamReader reader = new StreamReader(save_file_name.Text)) {
				load(reader);
			}
		}

		private Random _random = new Random();

		private void add_car_click(object sender, RoutedEventArgs e) {
			List<road_node> booths = new List<road_node>();
			foreach (road_node n in main_env.nodes) {
				if (n.type == node_type.booth) {
					booths.Add(n);
				}
			}
			road_node sb = booths[_random.Next(booths.Count)];
			main_env.cars.Add(new car() { on_lane = sb.out_lanes[_random.Next(sb.out_lanes.Count)] });
			main_env.InvalidateVisual();
		}

		private void run_checked(object sender, RoutedEventArgs e) {
			_timer.Start();
			_stw.Start();
		}
		private void run_unchecked(object sender, RoutedEventArgs e) {
			_timer.Stop();
			_stw.Stop();
		}

		private void clear_cars_Click(object sender, RoutedEventArgs e) {
			main_env.reset();
			main_env.InvalidateVisual();
		}

		private void thetaval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			main_env.theta = thetaval.Value;
		}
		private void b1val_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			main_env.b1 = b1val.Value;
		}
		private void b2val_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			main_env.b2 = b2val.Value;
		}
	}
}
