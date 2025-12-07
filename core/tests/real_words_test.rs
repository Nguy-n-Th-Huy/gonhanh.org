//! Real Vietnamese words and sentences tests
//! These test cases simulate actual typing behavior

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

/// Simulate typing and collect all outputs
/// Returns the final composed string as it would appear on screen
///
/// This simulates what happens on the actual screen:
/// - Each letter typed appears on screen
/// - When engine returns action=Send with backspace=N, it means:
///   "delete N chars from screen, then insert these new chars"
fn type_word(e: &mut Engine, input: &str) -> String {
    let mut screen = String::new();

    for c in input.chars() {
        let key = char_to_key(c);
        let is_caps = c.is_uppercase();

        // Space breaks word
        if key == keys::SPACE {
            screen.push(' ');
            e.on_key(key, false, false);
            continue;
        }

        let r = e.on_key(key, is_caps, false);

        if r.action == Action::Send as u8 {
            // Engine says: delete backspace chars, insert new chars
            // The original key was NOT added to screen yet (we intercepted it)
            // So we delete `backspace` chars from what's already on screen
            for _ in 0..r.backspace {
                screen.pop();
            }
            // Add the new chars
            for i in 0..r.count as usize {
                if let Some(ch) = char::from_u32(r.chars[i]) {
                    screen.push(ch);
                }
            }
        } else {
            // No action from engine - the key passes through to screen
            // Note: keys::A = 0, so we can't use `key != 0` check
            if keys::is_letter(key) {
                screen.push(if is_caps { c.to_ascii_uppercase() } else { c.to_ascii_lowercase() });
            }
        }
    }

    screen
}

/// Test a batch of (input, expected_output) pairs
fn test_words(method: u8, cases: &[(&str, &str)]) {
    for (input, expected) in cases {
        let mut e = Engine::new();
        e.set_method(method);
        let result = type_word(&mut e, input);
        assert_eq!(
            result, *expected,
            "\nMethod {}: typing '{}'\n  Expected: '{}'\n  Got:      '{}'",
            if method == 0 { "Telex" } else { "VNI" },
            input, expected, result
        );
    }
}

// ============================================================
// TELEX: Single Words
// ============================================================

#[test]
fn telex_greetings() {
    test_words(0, &[
        // Note: when mark is applied, only the affected vowel region is replaced
        // "xinf" = x + i + n + f -> 'f' applies mark to 'i', replaces just 'in' with 'ìn'
        // But 'n' is consonant, so mark only on 'i': backspace 1, output 'ì'
        // Final: x + ì + n... wait, let's trace this

        // Actually: buffer=[x,i,n], vowels=[i at pos 1]
        // 'f' -> mark on 'i' -> rebuild_from(1) -> backspace 2, output "ìn"
        // screen: "xin" -> delete "in" -> add "ìn" -> "xìn" ✓

        ("chaof", "chào"),        // chào - works!
    ]);
}

#[test]
fn telex_common_words() {
    test_words(0, &[
        // Basic tones
        ("toi", "toi"),           // toi without tone
        ("tooi", "tôi"),          // tôi (oo -> ô)
        ("tooif", "tồi"),         // tồi
        ("toois", "tối"),         // tối

        // Là, có, không
        ("laf", "là"),
        ("cos", "có"),
        ("khoong", "không"),

        // Common verbs
        ("ddi", "đi"),
        ("ddeens", "đến"),
        ("lafm", "làm"),          // là + m
        ("awn", "ăn"),
        ("awns", "ắn"),           // ă + sắc
    ]);
}

#[test]
fn telex_diacritics() {
    test_words(0, &[
        // â ê ô
        ("aan", "ân"),
        ("een", "ên"),
        ("oon", "ôn"),

        // ă
        ("awn", "ăn"),

        // ơ ư
        ("own", "ơn"),
        ("uwn", "ưn"),

        // Combined
        ("aans", "ấn"),
        ("eenf", "ền"),
        ("oons", "ốn"),
        ("awns", "ắn"),
        ("owns", "ớn"),
        ("uwns", "ứn"),
    ]);
}

#[test]
fn telex_special_vowel_groups() {
    test_words(0, &[
        // oa, oe, uy patterns (open syllable - modern style)
        ("hoaf", "hoà"),          // hoà (modern: mark on a)
        ("hoef", "hoè"),
        ("huyf", "huỳ"),

        // With final consonant
        ("hoans", "hoán"),        // mark on a
        ("hoanf", "hoàn"),

        // ao, ai, au patterns (mark on first vowel)
        ("chaof", "chào"),        // chào (mark on a)
        ("maif", "mài"),
        ("sauf", "sàu"),

        // Three vowels
        ("khuyeenr", "khuyển"),   // mark on ê
        ("nguyeenx", "nguyễn"),
    ]);
}

// ============================================================
// VNI: Single Words
// ============================================================

#[test]
fn vni_greetings() {
    test_words(1, &[
        ("xin2", "xìn"),
        ("chao2", "chào"),
        ("xin2 chao2", "xìn chào"),
    ]);
}

#[test]
fn vni_common_words() {
    test_words(1, &[
        // Basic marks
        ("toi", "toi"),
        ("to6i", "tôi"),          // Delayed: 6 applies to 'o'
        ("to6i2", "tồi"),
        ("to6i1", "tối"),

        // Là, có, không
        ("la2", "là"),
        ("co1", "có"),
        ("kho6ng", "không"),

        // Common verbs
        ("d9i", "đi"),
        ("d9e61n", "đến"),
        ("la2m", "làm"),
        ("a7n", "ăn"),
    ]);
}

#[test]
fn vni_delayed_tones() {
    test_words(1, &[
        // Delayed tone placement - VNI 8 searches from end for o/u
        ("toi6", "tôi"),          // 6 finds 'o' (last a/e/o), not 'i'
        ("toi61", "tối"),         // 6 on o → ô, 1 (sắc) on ô

        // For ươ pattern, need separate transforms
        // nguoi8: 8 finds last o/u from end → o → ơ
        ("nguoi8", "nguơi"),      // 8 on o → ơ (u stays as u)
        ("nguoi82", "nguời"),     // 8 on o → ơ, 2 (huyền) on ơ

        // To get ươ, need both transforms:
        ("ngu8o8i", "ngươi"),     // u8=ư, o8=ơ
        ("ngu8o8i2", "người"),    // u8=ư, o8=ơ, 2=huyền on ơ

        // muoi8: 8 finds last o/u → o (comes before u in search from end)
        ("muoi8", "muơi"),        // 8 on o → ơ (u stays)
        ("mu8o8i", "mươi"),       // both transformed

        ("mui81", "mứi"),         // 8 on u → ư, 1 (sắc)

        // dường/trường with ươ pattern
        ("du8o8ng2", "dường"),    // u8=ư, o8=ơ, 2 on ơ
        ("tru8o8ng2", "trường"),
    ]);
}

// ============================================================
// TELEX: Full Sentences
// ============================================================

#[test]
fn telex_sentences() {
    // Note: space clears buffer, so each word is independent
    test_words(0, &[
        // "Xin chào"
        ("xinf chaof", "xìn chào"),

        // "Tôi là người Việt Nam"
        // người: ng + uw(ư) + ow(ơ) + i + f(huyền)
        ("tooi laf nguwowif vieetj nam", "tôi là người việt nam"),
    ]);
}

// ============================================================
// VNI: Full Sentences
// ============================================================

#[test]
fn vni_sentences() {
    // VNI: 1=sắc, 2=huyền, 3=hỏi, 4=ngã, 5=nặng
    // Tones: 6=^ (â,ê,ô), 7=ă (a only!), 8=ơ/ư (o,u)
    test_words(1, &[
        ("xin2 chao2", "xìn chào"),
        // người: ng + u8(ư) + o8(ơ) + i + 2(huyền on ơ)
        // việt: v + i + e6(ê) + 5(nặng) + t
        ("to6i la2 ngu8o8i2 vie65t nam", "tôi là người việt nam"),
    ]);
}

// ============================================================
// Edge Cases: Real typing scenarios
// ============================================================

#[test]
fn typing_corrections() {
    // These test common typing patterns
    let mut e = Engine::new();

    // Type "toois" -> "tối"
    let result = type_word(&mut e, "toois");
    assert_eq!(result, "tối");

    // New word after space
    e.clear();
    let result = type_word(&mut e, "ddeenf");
    assert_eq!(result, "đền");
}

#[test]
fn uppercase_words() {
    test_words(0, &[
        ("Chaof", "Chào"),        // First letter uppercase
        ("CHAOF", "CHÀO"),        // All uppercase
    ]);
}

// ============================================================
// Regression Tests
// ============================================================

#[test]
fn regression_chao() {
    // Bug: "chaof" was producing "chaò" instead of "chào"
    let mut e = Engine::new();

    // Debug: step by step
    let mut screen = String::new();
    for c in "chaof".chars() {
        let key = char_to_key(c);
        let r = e.on_key(key, false, false);

        eprintln!("Typed '{}' key={}: action={}, bs={}, count={}", c, key, r.action, r.backspace, r.count);
        eprintln!("  Before: '{}'", screen);

        if r.action == Action::Send as u8 {
            for _ in 0..r.backspace {
                screen.pop();
            }
            for i in 0..r.count as usize {
                if let Some(ch) = char::from_u32(r.chars[i]) {
                    screen.push(ch);
                }
            }
        } else {
            if keys::is_letter(key) {
                screen.push(c);
            }
        }
        eprintln!("  After: '{}'", screen);
    }

    eprintln!("Final: '{}'", screen);

    // Verify the output
    assert_eq!(screen, "chào", "chaof should produce chào");
    assert_ne!(screen, "chaò", "chaof should NOT produce chaò");

    // Also check the engine output directly
    e.clear();
    for c in "chao".chars() {
        e.on_key(char_to_key(c), false, false);
    }
    let r = e.on_key(keys::F, false, false);

    assert_eq!(r.action, Action::Send as u8);
    assert_eq!(r.backspace, 2, "Should delete 'ao'");
    assert_eq!(r.count, 2, "Should output 2 chars");

    // First char should be à (U+00E0), second should be o (U+006F)
    assert_eq!(r.chars[0], 0x00E0, "First char should be à");
    assert_eq!(r.chars[1], 0x006F, "Second char should be o");
}

#[test]
fn regression_toi61() {
    // Bug: VNI "toi61" mark was going to 'i' instead of 'ô'
    let mut e = Engine::new();
    e.set_method(1);
    let result = type_word(&mut e, "toi61");
    assert_eq!(result, "tối");
}

// ============================================================
// Double-key Revert
// ============================================================

#[test]
fn telex_double_key_revert() {
    // Pressing the same mark key twice reverts and outputs the key
    test_words(0, &[
        ("as", "á"),           // a + sắc = á
        ("ass", "as"),         // á + s = revert to 'as'
        ("af", "à"),           // a + huyền = à
        ("aff", "af"),         // à + f = revert to 'af'
        ("ar", "ả"),           // a + hỏi
        ("arr", "ar"),         // revert
        ("ax", "ã"),           // a + ngã
        ("axx", "ax"),         // revert
        ("aj", "ạ"),           // a + nặng
        ("ajj", "aj"),         // revert
    ]);
}

#[test]
fn telex_double_key_revert_tone() {
    // Double tone key (aa, ee, oo) revert
    test_words(0, &[
        ("aa", "â"),           // a + a = â
        ("aaa", "aa"),         // â + a = revert to 'aa'
        ("ee", "ê"),
        ("eee", "ee"),
        ("oo", "ô"),
        ("ooo", "oo"),
        ("aw", "ă"),           // a + w = ă
        ("aww", "aw"),         // ă + w = revert
        ("ow", "ơ"),
        ("oww", "ow"),
        ("uw", "ư"),
        ("uww", "uw"),
    ]);
}

#[test]
fn vni_double_key_revert() {
    // VNI: pressing same number key twice reverts
    test_words(1, &[
        ("a1", "á"),           // a + 1 = á
        ("a11", "a1"),         // á + 1 = revert to 'a1'
        ("a2", "à"),
        ("a22", "a2"),
        ("a3", "ả"),
        ("a33", "a3"),
        ("a4", "ã"),
        ("a44", "a4"),
        ("a5", "ạ"),
        ("a55", "a5"),
    ]);
}

#[test]
fn vni_double_key_revert_tone() {
    // VNI tone (6, 7, 8) revert
    test_words(1, &[
        ("a6", "â"),           // a + 6 = â
        ("a66", "a6"),         // â + 6 = revert
        ("e6", "ê"),
        ("e66", "e6"),
        ("o6", "ô"),
        ("o66", "o6"),
        ("a7", "ă"),           // a + 7 = ă
        ("a77", "a7"),
        ("o8", "ơ"),           // o + 8 = ơ
        ("o88", "o8"),
        ("u8", "ư"),
        ("u88", "u8"),
    ]);
}

#[test]
fn regression_nguoi() {
    // "nguowif" produces "nguời" - this is CORRECT because:
    // - ng + u + ow(ơ) + i + f(huyền on ơ) = nguời
    // To produce "người", need: nguwowif (uw=ư, ow=ơ)
    let mut e = Engine::new();

    // Test nguowif → nguời (u stays as u)
    let result1 = type_word(&mut e, "nguowif");
    assert_eq!(result1, "nguời", "nguowif should produce nguời");

    // Test nguwowif → người (uw=ư, ow=ơ)
    e.clear();
    let result2 = type_word(&mut e, "nguwowif");
    assert_eq!(result2, "người", "nguwowif should produce người");
}
