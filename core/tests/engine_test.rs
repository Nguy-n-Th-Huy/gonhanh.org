//! Comprehensive tests for Vietnamese IME Engine
//! Test cases based on: https://vi.wikipedia.org/wiki/Quy_tắc_đặt_dấu_thanh_của_chữ_Quốc_ngữ

use gonhanh_core::data::keys;
use gonhanh_core::engine::{Action, Engine};

// ============================================================
// Test Helpers
// ============================================================

fn char_to_key(c: char) -> u16 {
    match c.to_ascii_lowercase() {
        'a' => keys::A, 'b' => keys::B, 'c' => keys::C, 'd' => keys::D,
        'e' => keys::E, 'f' => keys::F, 'g' => keys::G, 'h' => keys::H,
        'i' => keys::I, 'j' => keys::J, 'k' => keys::K, 'l' => keys::L,
        'm' => keys::M, 'n' => keys::N, 'o' => keys::O, 'p' => keys::P,
        'q' => keys::Q, 'r' => keys::R, 's' => keys::S, 't' => keys::T,
        'u' => keys::U, 'v' => keys::V, 'w' => keys::W, 'x' => keys::X,
        'y' => keys::Y, 'z' => keys::Z,
        '0' => keys::N0, '1' => keys::N1, '2' => keys::N2, '3' => keys::N3,
        '4' => keys::N4, '5' => keys::N5, '6' => keys::N6, '7' => keys::N7,
        '8' => keys::N8, '9' => keys::N9,
        ' ' => keys::SPACE,
        _ => 0,
    }
}

fn type_str(e: &mut Engine, s: &str) -> Vec<gonhanh_core::engine::Result> {
    s.chars()
        .map(|c| e.on_key(char_to_key(c), c.is_uppercase(), false))
        .collect()
}

/// Get first output char from result
fn first_char(r: &gonhanh_core::engine::Result) -> Option<char> {
    if r.action == Action::Send as u8 && r.count > 0 {
        char::from_u32(r.chars[0])
    } else {
        None
    }
}

/// Run batch test: input -> expected output char
fn test_batch(method: u8, cases: &[(&str, char)]) {
    for (input, expected) in cases {
        let mut e = Engine::new();
        e.set_method(method);
        let results = type_str(&mut e, input);
        let last = results.last().unwrap();
        assert_eq!(
            first_char(last), Some(*expected),
            "Method {}: '{}' should produce '{}'", method, input, expected
        );
    }
}

/// Run batch test with backspace verification
fn test_batch_full(method: u8, cases: &[(&str, char, u8, u8)]) {
    for (input, expected_char, expected_bs, expected_count) in cases {
        let mut e = Engine::new();
        e.set_method(method);
        let results = type_str(&mut e, input);
        let last = results.last().unwrap();
        assert_eq!(
            first_char(last), Some(*expected_char),
            "Method {}: '{}' char mismatch", method, input
        );
        assert_eq!(
            last.backspace, *expected_bs,
            "Method {}: '{}' backspace mismatch", method, input
        );
        assert_eq!(
            last.count, *expected_count,
            "Method {}: '{}' count mismatch", method, input
        );
    }
}

// ============================================================
// TELEX: Marks (s/f/r/x/j)
// ============================================================

#[test]
fn telex_marks() {
    test_batch(0, &[
        ("as", 'á'),   // sắc
        ("af", 'à'),   // huyền
        ("ar", 'ả'),   // hỏi
        ("ax", 'ã'),   // ngã
        ("aj", 'ạ'),   // nặng
        ("es", 'é'),
        ("ef", 'è'),
        ("is", 'í'),
        ("os", 'ó'),
        ("us", 'ú'),
        ("ys", 'ý'),
    ]);
}

// ============================================================
// TELEX: Tones (aa/ee/oo/aw/ow/uw/dd)
// ============================================================

#[test]
fn telex_tones() {
    test_batch(0, &[
        ("aa", 'â'),   // hat
        ("ee", 'ê'),
        ("oo", 'ô'),
        ("aw", 'ă'),   // breve
        ("ow", 'ơ'),   // horn
        ("uw", 'ư'),
        ("dd", 'đ'),
    ]);
}

// ============================================================
// TELEX: Combined (tone + mark)
// ============================================================

#[test]
fn telex_combined() {
    test_batch(0, &[
        ("aas", 'ấ'),  // â + sắc
        ("aaf", 'ầ'),  // â + huyền
        ("aar", 'ẩ'),  // â + hỏi
        ("aax", 'ẫ'),  // â + ngã
        ("aaj", 'ậ'),  // â + nặng
        ("ees", 'ế'),
        ("eef", 'ề'),
        ("oos", 'ố'),
        ("oof", 'ồ'),
        ("ooj", 'ộ'),
        ("aws", 'ắ'),  // ă + sắc
        ("awf", 'ằ'),
        ("ows", 'ớ'),  // ơ + sắc
        ("owf", 'ờ'),
        ("uws", 'ứ'),  // ư + sắc
        ("uwf", 'ừ'),
    ]);
}

// ============================================================
// VNI: Marks (1-5)
// ============================================================

#[test]
fn vni_marks() {
    test_batch(1, &[
        ("a1", 'á'),   // sắc
        ("a2", 'à'),   // huyền
        ("a3", 'ả'),   // hỏi
        ("a4", 'ã'),   // ngã
        ("a5", 'ạ'),   // nặng
        ("e1", 'é'),
        ("e2", 'è'),
        ("i1", 'í'),
        ("o1", 'ó'),
        ("u1", 'ú'),
        ("y1", 'ý'),
    ]);
}

// ============================================================
// VNI: Tones (6/7/8/9)
// ============================================================

#[test]
fn vni_tones() {
    test_batch(1, &[
        ("a6", 'â'),   // hat (^)
        ("e6", 'ê'),
        ("o6", 'ô'),
        ("a7", 'ă'),   // breve
        ("o8", 'ơ'),   // horn
        ("u8", 'ư'),
        ("d9", 'đ'),
    ]);
}

// ============================================================
// VNI: Combined (tone + mark)
// ============================================================

#[test]
fn vni_combined() {
    test_batch(1, &[
        ("a61", 'ấ'),  // â + sắc
        ("a62", 'ầ'),  // â + huyền
        ("a63", 'ẩ'),  // â + hỏi
        ("a64", 'ẫ'),  // â + ngã
        ("a65", 'ậ'),  // â + nặng
        ("e61", 'ế'),
        ("e62", 'ề'),
        ("o61", 'ố'),
        ("o62", 'ồ'),
        ("o65", 'ộ'),
        ("a71", 'ắ'),  // ă + sắc
        ("a72", 'ằ'),
        ("o81", 'ớ'),  // ơ + sắc
        ("o82", 'ờ'),
        ("u81", 'ứ'),  // ư + sắc
        ("u82", 'ừ'),
    ]);
}

// ============================================================
// VNI: Delayed tone (tone after multiple chars)
// ============================================================

#[test]
fn vni_delayed_tone() {
    // Tone finds correct vowel in buffer
    test_batch_full(1, &[
        // "toi6" -> ^ on 'o', not 'i'
        ("toi6", 'ô', 2, 2),    // backspace 2 (oi), output 2 (ôi)
        // "tuoi6" -> ^ on 'o'
        ("tuoi6", 'ô', 2, 2),
        // "nguoi8" -> horn on 'o'
        ("nguoi8", 'ơ', 2, 2),
        // "mui8" -> horn on 'u'
        ("mui8", 'ư', 2, 2),
    ]);
}

#[test]
fn vni_delayed_tone_with_final() {
    // "uong6" -> ^ on 'o', rebuilds "ông"
    let mut e = Engine::new();
    e.set_method(1);
    let results = type_str(&mut e, "uong6");
    let last = results.last().unwrap();
    assert_eq!(first_char(last), Some('ô'));
    // Note: actual backspace/count depends on implementation
}

// ============================================================
// VNI: Delayed mark (mark after tone)
// ============================================================

#[test]
fn vni_delayed_mark() {
    test_batch(1, &[
        ("toi61", 'ố'),   // tối
        ("toi62", 'ồ'),   // tồi
        ("nguoi82", 'ờ'), // người (huyền on ơ)
        ("duong82", 'ờ'), // dường
        ("sua81", 'ứ'),   // sứa
        ("an71", 'ắ'),    // ắn
        ("uong61", 'ố'),  // uống
    ]);
}

// ============================================================
// Mark Position: 1 vowel
// ============================================================

#[test]
fn mark_pos_single_vowel() {
    // Mark always on the single vowel
    test_batch(0, &[
        ("as", 'á'),
        ("bas", 'á'),
        ("tas", 'á'),
        ("mas", 'á'),
    ]);
}

// ============================================================
// Mark Position: 2 vowels + final consonant
// ============================================================

#[test]
fn mark_pos_two_vowels_closed() {
    // 2 vowels + consonant -> mark on 2nd vowel
    test_batch(0, &[
        ("toans", 'á'),   // toán (mark on a, not o)
        ("hoangs", 'á'),  // hoáng
        ("oans", 'á'),    // oán
        ("uons", 'ó'),    // uốn (mark on o)
        ("ieens", 'ế'),   // tiến (mark on e)
    ]);
}

// ============================================================
// Mark Position: 2 vowels open (oa, oe, uy) - Modern style
// ============================================================

#[test]
fn mark_pos_two_vowels_open_modern() {
    // Modern style: mark on 2nd vowel
    test_batch(0, &[
        ("hoaf", 'à'),    // hoà (mark on a)
        ("oaf", 'à'),     // oà
        ("hoef", 'è'),    // hoè
        ("oef", 'è'),     // oè
        ("huyf", 'ỳ'),    // huỳ (mark on y)
        ("uyf", 'ỳ'),
        ("quaf", 'à'),    // quà (mark on a)
        ("quas", 'á'),    // quá
    ]);
}

// ============================================================
// Mark Position: 2 vowels open - Old style
// ============================================================

#[test]
fn mark_pos_two_vowels_open_old() {
    // Old style: mark on 1st vowel
    let cases = &[
        ("hoaf", 'ò'),    // hòa (mark on o)
        ("oaf", 'ò'),     // òa
        ("hoef", 'ò'),    // hòe (mark on o)
        ("huyf", 'ù'),    // hùy (mark on u)
    ];

    for (input, expected) in cases {
        let mut e = Engine::new();
        e.set_method(0);
        e.set_modern(false);  // Old style
        let results = type_str(&mut e, input);
        let last = results.last().unwrap();
        assert_eq!(
            first_char(last), Some(*expected),
            "Old style: '{}' should produce '{}'", input, expected
        );
    }
}

// ============================================================
// Mark Position: 3+ vowels
// ============================================================

#[test]
fn mark_pos_three_vowels() {
    // 3 vowels -> mark on appropriate vowel
    // Note: "khuyen" without 'ee' has plain 'e', so 'r' gives 'ẻ' not 'ể'
    test_batch(0, &[
        ("khuyeenr", 'ể'),  // khuyển (ee -> ê, then r -> ể)
        ("nguyeenx", 'ễ'),  // nguyễn
    ]);
}

#[test]
fn mark_pos_khuyu() {
    // "khuyu" has 3 vowels: u, y, u -> mark on y (middle)
    let mut e = Engine::new();
    let results = type_str(&mut e, "khuyuf");
    let last = results.last().unwrap();
    assert_eq!(first_char(last), Some('ỳ'));
}

// ============================================================
// Mark Position: Special vowel pairs (iê, yê, uô, ươ)
// ============================================================

#[test]
fn mark_pos_special_pairs() {
    // These pairs with tones: mark goes on vowel with tone
    test_batch(0, &[
        ("tiees", 'ế'),   // tiến -> ee gives ê, s gives ế
        ("yees", 'ế'),    // yêu -> mark on ê
        ("muoons", 'ố'),  // muốn -> oo gives ô, s gives ố
        ("duowcj", 'ợ'),  // được -> ow gives ơ, j gives ợ (nặng)
    ]);
}

// ============================================================
// Words: Common Vietnamese words (Telex)
// ============================================================

#[test]
fn words_telex() {
    test_batch(0, &[
        // Single syllable
        ("chaof", 'à'),   // chào - "ao" pattern, mark on 'a'
        ("laf", 'à'),     // là
        ("cos", 'ó'),     // có (s = sắc)

        // With tones
        ("vieetj", 'ệ'),  // việt
        ("naams", 'ấ'),   // nấm -> mark on â
    ]);
}

#[test]
fn words_telex_dd() {
    // "dd" produces đ, then 'i' is just added to buffer
    let mut e = Engine::new();
    type_str(&mut e, "dd");  // đ
    let r = e.on_key(char_to_key('i'), false, false);
    // 'i' is a normal key, no output
    assert_eq!(r.action, Action::None as u8);
}

// ============================================================
// Words: Common Vietnamese words (VNI)
// ============================================================

#[test]
fn words_vni() {
    test_batch(1, &[
        ("chao2", 'à'),   // chào - mark on 'a' (ao pattern)
        ("la2", 'à'),     // là
        ("co1", 'ó'),     // có
        ("vie65", 'ệ'),   // việ (6=ê, 5=nặng) - without 't' to get last output
    ]);
}

#[test]
fn words_vni_d9() {
    // "d9" produces đ, then 'i' is just added to buffer
    let mut e = Engine::new();
    e.set_method(1);
    type_str(&mut e, "d9");  // đ
    let r = e.on_key(char_to_key('i'), false, false);
    assert_eq!(r.action, Action::None as u8);
}

// ============================================================
// Edge Cases: Uppercase
// ============================================================

#[test]
fn uppercase() {
    // Telex uppercase
    let mut e = Engine::new();
    e.on_key(keys::A, true, false);  // A (caps)
    let r = e.on_key(keys::S, false, false);
    assert_eq!(first_char(&r), Some('Á'));

    // VNI uppercase
    let mut e = Engine::new();
    e.set_method(1);
    e.on_key(keys::A, true, false);
    let r = e.on_key(keys::N1, false, false);
    assert_eq!(first_char(&r), Some('Á'));
}

// ============================================================
// Edge Cases: Break keys
// ============================================================

#[test]
fn break_keys() {
    let mut e = Engine::new();
    type_str(&mut e, "a ");  // space breaks
    let r = e.on_key(keys::S, false, false);
    assert_eq!(r.action, Action::None as u8);

    let mut e = Engine::new();
    type_str(&mut e, "a");
    e.on_key(keys::DELETE, false, false);  // backspace
    let r = e.on_key(keys::S, false, false);
    assert_eq!(r.action, Action::None as u8);
}

// ============================================================
// Edge Cases: Ctrl key
// ============================================================

#[test]
fn ctrl_clears() {
    let mut e = Engine::new();
    e.on_key(keys::A, false, false);
    e.on_key(keys::C, false, true);  // Ctrl+C
    let r = e.on_key(keys::S, false, false);
    assert_eq!(r.action, Action::None as u8);
}

// ============================================================
// Edge Cases: Disabled engine
// ============================================================

#[test]
fn disabled() {
    let mut e = Engine::new();
    e.set_enabled(false);
    let r = e.on_key(keys::A, false, false);
    assert_eq!(r.action, Action::None as u8);
}

// ============================================================
// Edge Cases: Remove mark (z/0)
// ============================================================

#[test]
fn remove_mark_telex() {
    let mut e = Engine::new();
    type_str(&mut e, "as");  // á
    let r = e.on_key(keys::Z, false, false);
    assert_eq!(first_char(&r), Some('a'));  // back to a
}

#[test]
fn remove_mark_vni() {
    let mut e = Engine::new();
    e.set_method(1);
    type_str(&mut e, "a1");  // á
    let r = e.on_key(keys::N0, false, false);
    assert_eq!(first_char(&r), Some('a'));
}

// ============================================================
// Regression: Previous bugs
// ============================================================

#[test]
fn regression_toi61() {
    // Bug: mark was going to 'i' instead of 'ô'
    let mut e = Engine::new();
    e.set_method(1);
    let results = type_str(&mut e, "toi61");
    let last = results.last().unwrap();
    assert_eq!(last.chars[0], 'ố' as u32);
    assert_ne!(last.chars[0], 'í' as u32);  // NOT í
}
