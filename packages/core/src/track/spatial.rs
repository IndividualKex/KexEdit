use std::collections::HashSet;

use crate::sim::Point;

use super::section::{Section, SectionLink};

const SPATIAL_TOLERANCE: f32 = 0.01;
const DIRECTION_THRESHOLD: f32 = 0.9;

pub fn compute_spatial_continuations(
    points: &[Point],
    sections: &mut [Section],
    traversal_order: &[i32],
) {
    let traversal_set: HashSet<i32> = traversal_order.iter().copied().collect();

    for i in 0..sections.len() {
        if !sections[i].is_valid() {
            continue;
        }

        if !sections[i].next.is_valid() {
            if let Some(link) = find_spatial_match(points, sections, i, true, &traversal_set) {
                sections[i].next = link;
            }
        }

        if !sections[i].prev.is_valid() {
            if let Some(link) = find_spatial_match(points, sections, i, false, &traversal_set) {
                sections[i].prev = link;
            }
        }
    }
}

fn find_spatial_match(
    points: &[Point],
    sections: &[Section],
    section_idx: usize,
    is_next: bool,
    traversal_set: &HashSet<i32>,
) -> Option<SectionLink> {
    let section = &sections[section_idx];
    if !section.is_valid() {
        return None;
    }

    let our_point_idx = if is_next {
        section.end_index as usize
    } else {
        section.start_index as usize
    };

    if our_point_idx >= points.len() {
        return None;
    }

    let our_point = &points[our_point_idx];
    let our_pos = our_point.heart_position;
    let our_dir = our_point.direction;

    let mut best_match: Option<SectionLink> = None;
    let mut best_dist = f32::MAX;
    let mut best_is_cosmetic = false;

    for (i, candidate) in sections.iter().enumerate() {
        if i == section_idx || !candidate.is_valid() {
            continue;
        }

        let cand_start_idx = candidate.start_index as usize;
        let cand_end_idx = candidate.end_index as usize;

        if cand_start_idx >= points.len() || cand_end_idx >= points.len() {
            continue;
        }

        let cand_start = &points[cand_start_idx];
        let cand_end = &points[cand_end_idx];

        let is_cosmetic = !traversal_set.contains(&(i as i32));

        if is_next {
            let dist_start = (our_pos - cand_start.heart_position).magnitude();
            let dir_dot_start = our_dir.dot(cand_start.direction);

            if dist_start < SPATIAL_TOLERANCE
                && dir_dot_start > DIRECTION_THRESHOLD
                && is_better_match(dist_start, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, true, false));
                best_dist = dist_start;
                best_is_cosmetic = is_cosmetic;
            }

            let dist_end = (our_pos - cand_end.heart_position).magnitude();
            let dir_dot_end = our_dir.dot(cand_end.direction);

            if dist_end < SPATIAL_TOLERANCE
                && dir_dot_end < -DIRECTION_THRESHOLD
                && is_better_match(dist_end, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, false, true));
                best_dist = dist_end;
                best_is_cosmetic = is_cosmetic;
            }
        } else {
            let dist_end = (our_pos - cand_end.heart_position).magnitude();
            let dir_dot_end = our_dir.dot(cand_end.direction);
            let dist_start = (our_pos - cand_start.heart_position).magnitude();
            let dir_dot_start = our_dir.dot(cand_start.direction);

            if dist_end < SPATIAL_TOLERANCE
                && dir_dot_end > DIRECTION_THRESHOLD
                && is_better_match(dist_end, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, false, false));
                best_dist = dist_end;
                best_is_cosmetic = is_cosmetic;
            }

            if dist_end < SPATIAL_TOLERANCE
                && dir_dot_end < -DIRECTION_THRESHOLD
                && is_better_match(dist_end, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, false, true));
                best_dist = dist_end;
                best_is_cosmetic = is_cosmetic;
            }

            if dist_start < SPATIAL_TOLERANCE
                && dir_dot_start < -DIRECTION_THRESHOLD
                && is_better_match(dist_start, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, true, true));
                best_dist = dist_start;
                best_is_cosmetic = is_cosmetic;
            }
        }
    }

    best_match
}

fn is_better_match(dist: f32, is_cosmetic: bool, best_dist: f32, best_is_cosmetic: bool) -> bool {
    if is_cosmetic && !best_is_cosmetic {
        return true;
    }
    if !is_cosmetic && best_is_cosmetic {
        return false;
    }
    dist < best_dist
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sim::Float3;

    fn make_test_point(arc: f32, pos: Float3, dir: Float3) -> Point {
        Point {
            heart_position: pos,
            direction: dir,
            normal: Float3::new(0.0, 1.0, 0.0),
            lateral: Float3::new(1.0, 0.0, 0.0),
            velocity: 10.0,
            normal_force: 1.0,
            lateral_force: 0.0,
            heart_arc: arc,
            spine_arc: arc,
            heart_advance: 0.0,
            friction_origin: 0.0,
            roll_speed: 0.0,
            heart_offset: 1.1,
            friction: 0.021,
            resistance: 2e-5,
        }
    }

    #[test]
    fn finds_same_direction() {
        let points = vec![
            make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
        ];

        let mut sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 3,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
        ];

        let traversal_order = vec![0, 1];
        compute_spatial_continuations(&points, &mut sections, &traversal_order);

        assert!(sections[0].next.is_valid());
        assert_eq!(sections[0].next.index, 1);
        assert!(sections[0].next.at_start());
        assert!(!sections[0].next.flip());

        assert!(sections[1].prev.is_valid());
        assert_eq!(sections[1].prev.index, 0);
        assert!(!sections[1].prev.at_start());
        assert!(!sections[1].prev.flip());
    }

    #[test]
    fn finds_opposite_direction() {
        let points = vec![
            make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(0.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, -1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, -1.0)),
        ];

        let mut sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 3,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
        ];

        let traversal_order = vec![0, 1];
        compute_spatial_continuations(&points, &mut sections, &traversal_order);

        assert!(sections[0].next.is_valid());
        assert_eq!(sections[0].next.index, 1);
        assert!(!sections[0].next.at_start());
        assert!(sections[0].next.flip());
    }

    #[test]
    fn prefers_cosmetic() {
        let points = vec![
            make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
        ];

        let mut sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 3,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 4,
                end_index: 5,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
        ];

        let traversal_order = vec![0, 2];
        compute_spatial_continuations(&points, &mut sections, &traversal_order);

        assert_eq!(sections[0].next.index, 1);
    }
}
