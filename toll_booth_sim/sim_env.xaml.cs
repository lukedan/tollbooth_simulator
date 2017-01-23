using System;
using System.Collections.Generic;
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

namespace toll_booth_sim {
	public static class utils {
		public static double point_line_distance_squared(Point p, Point l1, Point l2) {
			Vector v = l2 - l1, prp = new Vector(v.Y, -v.X), l1p = p - l1, l2p = p - l2;
			if (Vector.CrossProduct(l1p, prp) * Vector.CrossProduct(l2p, prp) < 0.0) {
				double d = l1p * prp;
				return d * d / prp.LengthSquared;
			}
			return Math.Min(l1p.LengthSquared, l2p.LengthSquared);
		}

		public static Vector project_onto(this Vector v, Vector axis) {
			return axis * ((v * axis) / axis.LengthSquared);
		}
		public static Vector with_length(this Vector v, double len) {
			return v * (len / v.Length);
		}

		public static bool sat_test_axis(Point s, Point e, Vector se, params Point[] ps) {
			bool pos = false, neg = false;
			for (int i = 0; i < ps.Length; ++i) {
				double
					sv = (ps[i] - s) * se,
					ev = (ps[i] - e) * se;
				if (sv * ev <= 0.0) {
					return true;
				}
				if (sv > 0.0) {
					if (neg) {
						return true;
					}
					pos = true;
				} else {
					if (pos) {
						return true;
					}
					neg = true;
				}
			}
			return false;
		}
		public static bool boxes_intersect(Point c1, Vector x1x, Vector x1y, Point c2, Vector x2x, Vector x2y) {
			return
				sat_test_axis(c1 + x1x, c1 - x1x, x1x, c2 + x2x + x2y, c2 + x2x - x2y, c2 - x2x + x2y, c2 - x2x - x2y) &&
				sat_test_axis(c1 + x1y, c1 - x1y, x1y, c2 + x2x + x2y, c2 + x2x - x2y, c2 - x2x + x2y, c2 - x2x - x2y) &&
				sat_test_axis(c2 + x2x, c2 - x2x, x2x, c1 + x1x + x1y, c1 + x1x - x1y, c1 - x1x + x1y, c1 - x1x - x1y) &&
				sat_test_axis(c2 + x2y, c2 - x2y, x2y, c1 + x1x + x1y, c1 + x1x - x1y, c1 - x1x + x1y, c1 - x1x - x1y);
		}
		public static bool box_trapezoid_intersect(
			Point bc, Vector bx, Vector by,
			Point tsc, Vector th, Vector tes, Vector teb
		) {
			if (!(
				sat_test_axis(bc + bx, bc - bx, bx, tsc + tes, tsc - tes, tsc + th + teb, tsc + th - teb) &&
				sat_test_axis(bc + by, bc - by, by, tsc + tes, tsc - tes, tsc + th + teb, tsc + th - teb) &&
				sat_test_axis(tsc, tsc + th, th, bc + bx + by, bc + bx - by, bc - bx - by, bc - bx + by)
			)) {
				return false;
			}
			Vector v1 = tes - teb, v2 = th - v1, e1 = teb + tes, e2 = th - e1;
			v1 += th;
			e1 += th;
			v1 = new Vector(v1.Y, -v1.X);
			v2 = new Vector(v2.Y, -v2.X);
			e1 = e1.project_onto(v1);
			e2 = e2.project_onto(v2);
			return
				sat_test_axis(tsc - tes, tsc - tes + e1, v1, bc + bx + by, bc + bx - by, bc - bx - by, bc - bx + by) &&
				sat_test_axis(tsc + tes, tsc + tes + e2, v2, bc + bx + by, bc + bx - by, bc - bx - by, bc - bx + by);
		}

		public static class bezier {
			public static Point get_pos(double t, Point p1, Point c1, Point p2, Point c2) {
				double omt = 1.0 - t;
				return (Point)(
					omt * omt * omt * (Vector)p1 +
					3 * t * omt * omt * (Vector)c1 +
					3 * t * t * omt * (Vector)c2 +
					t * t * t * (Vector)p2
				);
			}
			public static Vector get_derivative(double t, Point p1, Point c1, Point p2, Point c2) {
				double omt = 1.0 - t;
				return -3.0 * (omt * omt * (Vector)p1 + t * ((3.0 * t - 2) * (Vector)c2 - t * (Vector)p2) - ((3 * t - 4) * t + 1) * (Vector)c1);
			}
		}
	}

	public enum node_type {
		intermediate,
		booth,
		exit
	}
	public class road_node {
		public node_type type;
		public Point position;
		public double spawn_interval_min = 6.0, spawn_interval_max = 10.0;
		public double spawn_timer = 0.0;
		public int id = 0;

		public List<lane> in_lanes = new List<lane>(), out_lanes = new List<lane>();

		public const double drag_region_radius = 1.5;

		public bool hit_test(Point p) {
			return (p - position).LengthSquared < drag_region_radius * drag_region_radius;
		}
	}
	public class lane {
		public road_node from = null, to = null;
		public Point ctrl_from, ctrl_to;

		public const double thickness = 3.75, control_point_radius = 0.7;

		public List<Point> pattern_cache = null;
		public double min_dist = double.PositiveInfinity, length = double.NaN;
		public int num_cars = 0;


		public bool hit_test(Point p) {
			for (int i = 0; i < pattern_cache.Count; ++i) {
				if ((pattern_cache[i] - p).LengthSquared < 0.25 * thickness * thickness) {
					return true;
				}
			}
			return false;
		}

		public List<Point> get_pattern(int split = 100) {
			List<Point> result = new List<Point>();
			for (int i = 0; i <= split; ++i) {
				result.Add(utils.bezier.get_pos(i / (double)split, from.position, ctrl_from, to.position, ctrl_to));
			}
			return result;
		}

		public void make_cache(int split = 100) {
			pattern_cache = get_pattern(split);
			length = 0.0;
			for (int i = 1; i < pattern_cache.Count; ++i) {
				length += (pattern_cache[i] - pattern_cache[i - 1]).Length;
			}
		}
	}
	public class car {
		public lane on_lane = null;
		public double t = 0.0, dist_on_lane = 0.0, total_dist = 0.0;
		public double length = 5.0, width = 1.85;
		public double speed = 0.0, acceleration = 3.0, deceleration = 8.0;
		public bool valid = true, throttle = false, need_lane_decision = false;
		public int id = 0;

		public double fan_ratio = 0.05;
		public double detect_min = 1.0, detect_ratio = 1.2, predict_ratio = 0.5;
		public double max_speed = 15.0;

		public void update(double dt, List<car> others, StreamWriter evlogger) {
			throttle = true;
			Point pos;
			Vector dir, prp;
			get_pos_and_direction_full(out pos, out dir, out prp);
			double ratiov = detect_min + speed * detect_ratio;
			Vector nd = dir.with_length(ratiov);
			pos += dir;
			dir = nd;
			foreach (car c in others) {
				if (c != this) {
					Point opos;
					Vector odir, oprp;
					c.get_pos_and_direction_full(out opos, out odir, out oprp);
					Vector ond = odir.with_length(c.speed * predict_ratio);
					opos += ond;
					odir += ond;
					if (utils.box_trapezoid_intersect(opos, odir, oprp, pos, dir, prp, prp * (1.0 + ratiov * fan_ratio))) {
						throttle = false;
						break;
					}
				}
			}

			speed = Math.Max(0.0, Math.Min(max_speed, speed + (throttle ? acceleration : -deceleration) * dt));
			double dx = speed * dt;
			t += dx / utils.bezier.get_derivative(
				t, on_lane.from.position, on_lane.ctrl_from, on_lane.to.position, on_lane.ctrl_to
			).Length;
			dist_on_lane += dx;
			total_dist += dx;
			if (dist_on_lane < on_lane.min_dist) {
				on_lane.min_dist = dist_on_lane;
			}
			++on_lane.num_cars;
			need_lane_decision = false;
			if (t >= 1.0) {
				if (on_lane.to.type == node_type.exit || on_lane.to.out_lanes.Count == 0) {
					t = 1.0;
					valid = false;
				} else {
					t = dist_on_lane = 0.0;
					need_lane_decision = true;
				}
			}
		}
		public void make_lane_decision(road_node outn, double b1, double b2, double theta, Random r) {
			//List<double> ps = new List<double>();
			double minv = double.PositiveInfinity;
			foreach (lane l in outn.out_lanes) {
				minv = Math.Min(minv, l.length);
			}
			double maxp = 0.0;
			lane maxl = null;
			foreach (lane l in outn.out_lanes) {
				double curp = b1 * Math.Exp(theta * (l.length - minv)) + b2 / Math.Sqrt(l.num_cars + 1);
				//ps.Add(curp);
				if (curp > maxp) {
					maxl = l;
				}
			}
			on_lane = maxl;
			return;
			//double minx = double.PositiveInfinity;
			//foreach (double d in ps) {
			//	if (d > 0.0) {
			//		minx = Math.Min(minx, d);
			//	}
			//}
			//minx *= 0.2;
			//double totp = 0.0;
			//for (int i = 0; i < ps.Count; ++i) {
			//	if (ps[i] < minx) {
			//		ps[i] = minx;
			//	}
			//	totp += ps[i];
			//}

			//double v = r.NextDouble() * totp;
			//for (int i = 0; i < ps.Count; ++i) {
			//	if (v <= ps[i]) {
			//		on_lane = outn.out_lanes[i];
			//		return;
			//	}
			//	v -= ps[i];
			//}
			//on_lane = outn.out_lanes[outn.out_lanes.Count - 1];
		}
		public void make_lane_decision(double b1, double b2, double theta, Random r) {
			need_lane_decision = false;
			make_lane_decision(on_lane.to, b1, b2, theta, r);
		}

		public void get_pos_and_direction(out Point pos, out Vector dir) {
			pos = utils.bezier.get_pos(t, on_lane.from.position, on_lane.ctrl_from, on_lane.to.position, on_lane.ctrl_to);
			dir = utils.bezier.get_derivative(t, on_lane.from.position, on_lane.ctrl_from, on_lane.to.position, on_lane.ctrl_to);
			dir *= length * 0.5 / dir.Length;
		}
		public void get_pos_and_direction_full(out Point pos, out Vector dir, out Vector prp) {
			get_pos_and_direction(out pos, out dir);
			prp = new Vector(dir.Y, -dir.X) * (width / length);
		}
	}

	public partial class sim_env : UserControl {
		public sim_env() {
			InitializeComponent();
			_cache_trans();
		}

		public List<road_node> nodes = new List<road_node>();
		public List<lane> lanes = new List<lane>();
		public List<car> cars = new List<car>();

		public bool creating_lane = false;
		public int car_id_counter = 0;
		public int node_id_counter = 0;
		private road_node _lane_from;

		private int _drag_lane_cp = -1;

		private double _zoom = 20.0;
		private Vector _offset = new Vector();
		Transform _trans = Transform.Identity;
		GeneralTransform _invtrans = Transform.Identity;
		double _pixel_size = 0.0;
		private void _cache_trans() {
			TransformGroup tg = new TransformGroup();
			tg.Children.Add(new TranslateTransform(_offset.X, _offset.Y));
			tg.Children.Add(new ScaleTransform(_zoom, _zoom, 0.0, 0.0));
			_trans = tg;
			_invtrans = _trans.Inverse;
			_pixel_size = (_invtrans.Transform(new Point(0.0, 0.0)) - _invtrans.Transform(new Point(1.0, 0.0))).Length;
		}

		private Random _random = new Random();

		public object focused_object = null;
		public delegate void focus_change_delegate();
		public event focus_change_delegate focus_change;
		private Point _last_mouse_pos;
		private Vector _drag_offset;

		public Brush car_brush = new SolidColorBrush(Colors.Blue);

		public Brush control_point_brush = new SolidColorBrush(Colors.Yellow);
		public Pen lane_pen = new Pen(Brushes.LightGray, lane.thickness);

		public StreamWriter detailed_info_logger, event_logger;
		public double time = 0.0;

		public double b1 = 0.1, b2 = 0.1, theta = 0.1;

		public Brush
			booth_node_brush = new SolidColorBrush(Colors.Orange),
			exit_node_brush = new SolidColorBrush(Colors.LightGreen),
			node_brush = new SolidColorBrush(Colors.Gray);

		public Brush
			throttle_brush = new SolidColorBrush(Colors.Green),
			brakes_brush = new SolidColorBrush(Colors.Red);

		public void delete_node(road_node n) {
			foreach (lane l in n.in_lanes) {
				l.from.out_lanes.Remove(l);
				lanes.Remove(l);
			}
			foreach (lane l in n.out_lanes) {
				l.to.in_lanes.Remove(l);
				lanes.Remove(l);
			}
			nodes.Remove(n);
			if (focused_object != null) {
				focused_object = null;
				focus_change?.Invoke();
			}
			InvalidateVisual();
		}
		public void delete_lane(lane l) {
			l.from.out_lanes.Remove(l);
			l.to.in_lanes.Remove(l);
			lanes.Remove(l);
			if (focused_object != null) {
				focused_object = null;
				focus_change?.Invoke();
			}
			InvalidateVisual();
		}

		public bool try_spawn_car(car c) {
			Point p = c.on_lane.from.position;
			Vector
				deriv = utils.bezier.get_derivative(
					0.0, c.on_lane.from.position, c.on_lane.ctrl_from, c.on_lane.to.position, c.on_lane.ctrl_to
				), dir = deriv.with_length(0.5 * c.length), prp = new Vector(dir.Y, -dir.X) * (c.width / c.length);
			foreach (car oc in cars) {
				Point op;
				Vector od1, od2;
				oc.get_pos_and_direction_full(out op, out od1, out od2);
				if (utils.boxes_intersect(p, dir, prp, op, od1, od2)) {
					return false;
				}
			}
			return true;
		}
		public void update_noredraw(double dt) {
			foreach (lane l in lanes) {
				l.num_cars = 0;
				l.min_dist = double.PositiveInfinity;
			}

			if (detailed_info_logger != null) {
				detailed_info_logger.WriteLine("# {0}", time);
				foreach (car c in cars) {
					Point pos;
					Vector dir;
					c.get_pos_and_direction(out pos, out dir);
					detailed_info_logger.WriteLine("{0} {1} {2} {3} {4}", c.id, c.speed, pos.X, pos.Y, c.total_dist);
				}
			}

			foreach (car c in cars) {
				c.update(dt, cars, event_logger);
			}
			for (int i = cars.Count - 1; i >= 0; --i) {
				if (!cars[i].valid) {
					if (event_logger != null) {
						event_logger.WriteLine("{0} d {1} {2} {3}", time, cars[i].id, cars[i].on_lane.to.id, cars[i].total_dist);
					}
					cars[i] = cars[cars.Count - 1];
					cars.RemoveAt(cars.Count - 1);
				} else if (cars[i].need_lane_decision) {
					cars[i].make_lane_decision(b1, b2, theta, _random);
					if (event_logger != null) {
						event_logger.WriteLine("{0} c {1} {2}", time, cars[i].id, cars[i].on_lane.to.id);
					}
				}
			}
			foreach (road_node n in nodes) {
				if (n.type == node_type.booth) {
					n.spawn_timer -= dt;
					if (n.spawn_timer < 0.0) {
						n.spawn_timer += _random.NextDouble() * (n.spawn_interval_max - n.spawn_interval_min) + n.spawn_interval_min;
						car c = new car();
						c.make_lane_decision(n, b1, b2, theta, _random);
						if (try_spawn_car(c)) {
							c.id = car_id_counter++;
							cars.Add(c);
							if (event_logger != null) {
								event_logger.WriteLine("{0} s {1} {2} {3}", time, c.id, n.id, c.on_lane.to.id);
							}
						}
					}
				}
			}

			time += dt;
		}

		public void reset() {
			cars.Clear();
			car_id_counter = 0;
			time = 0.0;
		}

		private object _hit_test(Point p) {
			foreach (road_node n in nodes) {
				if (n.hit_test(p)) {
					return n;
				}
			}
			foreach (lane l in lanes) {
				if (l.hit_test(p)) {
					return l;
				}
			}
			return null;
		}

		private void _drag_test_magnify(ref Point p) {
			if (Keyboard.IsKeyDown(Key.LeftAlt)) {
				p = new Point(Math.Round(p.X), Math.Round(p.Y));
			}
		}

		protected override void OnMouseDown(MouseButtonEventArgs e) {
			base.OnMouseDown(e);

			if (e.ChangedButton == MouseButton.Left) {
				if (focused_object is lane) {
					_drag_lane_cp = -1;
					lane sell = (lane)focused_object;
					if ((_last_mouse_pos - sell.ctrl_from).LengthSquared < lane.control_point_radius * lane.control_point_radius) {
						_drag_offset = sell.ctrl_from - _last_mouse_pos;
						_drag_lane_cp = 0;
					}
					if ((_last_mouse_pos - sell.ctrl_to).LengthSquared < lane.control_point_radius * lane.control_point_radius) {
						_drag_offset = sell.ctrl_to - _last_mouse_pos;
						_drag_lane_cp = 1;
					}
				}
				if (_drag_lane_cp < 0) {
					object oldf = focused_object;
					focused_object = _hit_test(_last_mouse_pos);
					if (oldf != focused_object) {
						focus_change?.Invoke();
					}
					if (focused_object == null) {
						_drag_offset = _offset - (Vector)_last_mouse_pos;
					} else if (focused_object is road_node) {
						if (creating_lane) {
							_lane_from = (road_node)focused_object;
						} else {
							_drag_offset = ((road_node)focused_object).position - _last_mouse_pos;
						}
					}
				}
				InvalidateVisual();
			}
		}
		protected override void OnMouseUp(MouseButtonEventArgs e) {
			base.OnMouseUp(e);

			if (e.ChangedButton == MouseButton.Left) {
				road_node rn = _hit_test(_last_mouse_pos) as road_node;
				if (creating_lane && _lane_from != null && rn != null && _lane_from != rn) {
					lane l = new lane() {
						from = _lane_from,
						to = rn,
						ctrl_from = (Point)((2.0 * (Vector)_lane_from.position + (Vector)rn.position) / 3.0),
						ctrl_to = (Point)(((Vector)_lane_from.position + 2.0 * (Vector)rn.position) / 3.0)
					};
					_lane_from.out_lanes.Add(l);
					rn.in_lanes.Add(l);
					l.make_cache();
					lanes.Add(l);
					InvalidateVisual();
				}
			}
		}
		protected override void OnMouseMove(MouseEventArgs e) {
			base.OnMouseMove(e);

			Point newmouse = _invtrans.Transform(e.GetPosition(this));
			if (e.LeftButton == MouseButtonState.Pressed) {
				if (focused_object == null) {
					_offset = (Vector)(newmouse + _drag_offset);
					newmouse = _last_mouse_pos;
					_cache_trans();
				} else if (focused_object is road_node) {
					if (!creating_lane) {
						road_node focus = (road_node)focused_object;
						Point oldp = focus.position;
						focus.position = newmouse + _drag_offset;
						_drag_test_magnify(ref focus.position);
						Vector realdiff = focus.position - oldp;
						foreach (lane cl in focus.in_lanes) {
							cl.ctrl_to += realdiff;
							cl.make_cache();
						}
						foreach (lane cl in focus.out_lanes) {
							cl.ctrl_from += realdiff;
							cl.make_cache();
						}
					}
				} else if (focused_object is lane) {
					lane sell = (lane)focused_object;
					if (_drag_lane_cp > -1) {
						if (_drag_lane_cp == 0) {
							sell.ctrl_from = newmouse + _drag_offset;
							_drag_test_magnify(ref sell.ctrl_from);
						}
						if (_drag_lane_cp == 1) {
							sell.ctrl_to = newmouse + _drag_offset;
							_drag_test_magnify(ref sell.ctrl_to);
						}
						sell.make_cache();
					}
				}
				InvalidateVisual();
			}
			_last_mouse_pos = newmouse;
		}
		protected override void OnMouseWheel(MouseWheelEventArgs e) {
			base.OnMouseWheel(e);

			double deltaz = e.Delta * 0.01;
			_offset = (Vector)(_last_mouse_pos - (Vector)(_last_mouse_pos - _offset) * (_zoom + deltaz) / _zoom);
			_zoom += deltaz;
			_cache_trans();
			InvalidateVisual();
		}

		private void _draw_bezier_arrow(double t, Point p1, Point c1, Point p2, Point c2, double size, StreamGeometryContext ctx) {
			Vector dir = utils.bezier.get_derivative(t, p1, c1, p2, c2).with_length(size), prp = new Vector(dir.Y, -dir.X);
			Point pos = utils.bezier.get_pos(t, p1, c1, p2, c2);
			dir *= 1.5;
			ctx.BeginFigure(pos - dir + prp, false, false);
			ctx.LineTo(pos, true, true);
			ctx.LineTo(pos - dir - prp, true, true);
		}
		protected override void OnRender(DrawingContext dc) {
			dc.PushTransform(_trans);
			Pen black_line = new Pen(Brushes.Black, _pixel_size);

			StreamGeometry ticks = new StreamGeometry();
			Point p1 = _invtrans.Transform(new Point(0.0, 0.0)), p2 = _invtrans.Transform(new Point(ActualWidth, ActualHeight));
			using (StreamGeometryContext solid = ticks.Open()) {
				for (int y = (int)Math.Ceiling(p1.Y); y < p2.Y; ++y) {
					if (y % 10 == 0) {
						solid.BeginFigure(new Point(p1.X, y), false, false);
						solid.LineTo(new Point(p2.X, y), true, false);
					}
				}
				for (int x = (int)Math.Ceiling(p1.X); x < p2.X; ++x) {
					if (x % 10 == 0) {
						solid.BeginFigure(new Point(x, p1.Y), false, false);
						solid.LineTo(new Point(x, p2.Y), true, false);
					}
				}
			}
			dc.DrawGeometry(null, new Pen(Brushes.LightGray, _pixel_size), ticks);

			StreamGeometry beziers = new StreamGeometry();
			using (StreamGeometryContext ctx = beziers.Open()) {
				foreach (lane l in lanes) {
					ctx.BeginFigure(l.from.position, false, false);
					ctx.BezierTo(l.ctrl_from, l.ctrl_to, l.to.position, true, false);
				}
			}
			dc.DrawGeometry(null, lane_pen, beziers);
			dc.DrawGeometry(null, black_line, beziers);
			StreamGeometry innerbeziers = new StreamGeometry();
			using (StreamGeometryContext ctx = innerbeziers.Open()) {
				foreach (lane l in lanes) {
					_draw_bezier_arrow(1.0 / 3.0, l.from.position, l.ctrl_from, l.to.position, l.ctrl_to, 1.0, ctx);
					_draw_bezier_arrow(2.0 / 3.0, l.from.position, l.ctrl_from, l.to.position, l.ctrl_to, 1.0, ctx);
				}
			}
			dc.DrawGeometry(null, black_line, innerbeziers);
			if (focused_object is lane) {
				lane fl = (lane)focused_object;
				dc.DrawLine(black_line, fl.from.position, fl.ctrl_from);
				dc.DrawLine(black_line, fl.to.position, fl.ctrl_to);
				dc.DrawEllipse(control_point_brush, black_line, fl.ctrl_from, lane.control_point_radius, lane.control_point_radius);
				dc.DrawEllipse(control_point_brush, black_line, fl.ctrl_to, lane.control_point_radius, lane.control_point_radius);
			}

			foreach (road_node n in nodes) {
				Point p = _trans.Transform(n.position);
				dc.DrawEllipse(
					n.type == node_type.booth ? booth_node_brush : (n.type == node_type.exit ? exit_node_brush : node_brush),
					n == focused_object ? black_line : null,
					n.position, road_node.drag_region_radius, road_node.drag_region_radius
				);
				FormattedText lbl = new FormattedText(
					n.id.ToString(), System.Globalization.CultureInfo.CurrentCulture,
					FlowDirection.LeftToRight, new Typeface("Segoe UI"), 2.0, Brushes.Black
				);
				dc.DrawText(lbl, n.position - new Vector(lbl.Width, lbl.Height) * 0.5);
			}

			foreach (car c in cars) {
				Point pos;
				Vector dir, prp;
				c.get_pos_and_direction_full(out pos, out dir, out prp);

				Brush b = car_brush;
				foreach (car d in cars) {
					if (c == d) {
						continue;
					}
					Point np;
					Vector nd, nprp;
					d.get_pos_and_direction_full(out np, out nd, out nprp);
					if (utils.boxes_intersect(pos, dir, prp, np, nd, nprp)) {
						b = new SolidColorBrush(Colors.Red);
						break;
					}
				}

				StreamGeometry sg = new StreamGeometry();
				using (StreamGeometryContext ctx = sg.Open()) {
					//Point npos;
					//Vector ndir, nprp;
					//c.get_pos_and_direction_full(out npos, out ndir, out nprp);
					//double ratiov = c.detect_min + c.speed * c.detect_ratio;
					//Vector nd = ndir.with_length(ratiov);
					//npos += ndir;
					//ndir = nd;
					//Vector nnprp = prp * (1.0 + ratiov * c.fan_ratio);

					//ctx.BeginFigure(npos + nprp, false, true);
					//ctx.LineTo(npos + nd + nnprp, true, true);
					//ctx.LineTo(npos + nd - nnprp, true, true);
					//ctx.LineTo(npos - nprp, true, true);

					ctx.BeginFigure(pos + dir + prp, true, true);
					ctx.LineTo(pos + dir - prp, true, false);
					ctx.LineTo(pos - dir - prp, true, false);
					ctx.LineTo(pos - dir + prp, true, false);
				}
				dc.DrawGeometry(b, black_line, sg);
				dc.DrawEllipse(c.throttle ? throttle_brush : brakes_brush, null, pos, c.speed * 0.05, c.speed * 0.05);
			}

			dc.Pop();

			StreamGeometry legend = new StreamGeometry();
			Point cornerpt = new Point(ActualWidth - 10, ActualHeight - 10);
			using (StreamGeometryContext ctx = legend.Open()) {
				ctx.BeginFigure(cornerpt + new Vector(0.0, -5.0), false, false);
				ctx.LineTo(cornerpt, true, false);
				ctx.LineTo(cornerpt - new Vector(1.0 / _pixel_size, 0.0), true, false);
				ctx.LineTo(cornerpt - new Vector(1.0 / _pixel_size, 5.0), true, false);
			}
			dc.DrawGeometry(null, new Pen(Brushes.Black, 1.0), legend);
			FormattedText txt = new FormattedText(
				"1m", System.Globalization.CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12.0, Brushes.Black
			);
			dc.DrawText(txt, cornerpt - new Vector(txt.Width, txt.Height + 5.0));
		}
	}
}
