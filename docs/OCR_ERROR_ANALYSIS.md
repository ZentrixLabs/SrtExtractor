# Alien (1979) OCR Error Analysis

This document analyzes OCR errors found in the Alien (1979) Directors Cut extraction compared to OpenSubtitles reference.

## Key Findings

### 1. Complete Line Failures (Garbage Output)

Looking at the generated SRT vs OpenSubtitles, here are the major errors:

#### 7:11-7:14 (Subtitle #14)
**Generated OCR:**
```
PANq)Y, oo To \AS\VTR (I Y10
you look dead, man?
```

**Should be:**
```
Anybody ever tell you
you look dead, man?
```

**Analysis:** "Anybody ever tell you" became "PANq)Y, oo To \AS\VTR (I Y10" - complete misread, possibly unusual font or styling

---

#### 7:19-7:21 (Subtitle #16)
**Generated OCR:**
```
N[\ NV
forgot something, man.
```

**Should be:**
```
Now, I just
forgot something, man.
```

**Analysis:** "Now, I just" became "N[\ NV" - short words with unusual rendering

---

#### 7:33-7:37 (Subtitle #21)
**Generated OCR:**
```
ICEIRUE IR ER oo s IR0 = (o]O
has never been on
=I NCTo [N1E=I o] [CRIEAVET
```

**Should be:**
```
feel that the bonus situation
has never been on
an equitable level.
```

**Analysis:** "feel that the bonus situation" and "an equitable level" completely destroyed - possibly italic or small text

---

#### 7:36-7:39 (Subtitle #22)
**Generated OCR:**
```
Well, you'll get
W EIRYe I NN elelpli=Tei(=Te o]y
[LGCREEgY oloJo \VACIETS
```

**Should be:**
```
Well, you'll get
what you're contracted for
like everybody else.
```

**Analysis:** "what you're contracted for like everybody else" → garbage. Possible apostrophe issues?

---

#### 7:39-7:41 (Subtitle #23)
**Generated OCR:**
```
Yes, but everybody else
I (o] CRUETINIED
[ Monitor Beeping ]
```

**Should be:**
```
Yes, but everybody else
gets more than us.
```

**Analysis:** "gets more than us" became "I (o] CRUETINIED"

---

#### 7:41-7:45 (Subtitle #24)
**Generated OCR:**
```
DTN (o] (gl=Tg
WEQICRREL Qe RY/e]IN
```

**Should be:**
```
Dallas, Mother
wants to talk to you.
```

**Analysis:** Completely failed - character name (Dallas) misread

---

#### 7:46-7:48 (Subtitle #26)
**Generated OCR:**
```
(©]I¢- \VANe[=] o | (=117=To MM o101 9 s
```

**Should be:**
```
Okay, get dressed, huh?
```

**Analysis:** Complete garbage for simple phrase

---

#### 9:17-9:20 (Subtitle #34)
**Generated OCR:**
```
[ Voice Over Radlo, Indistinct |
```

**Should be:**
```
[ Voice Over Radio, Indistinct ]
```

**Analysis:** "Radio" became "Radlo" - OCR confusion with similar characters (i/l)

---

#### 9:32-9:36 (Subtitle #37)
**Generated OCR:**
```
LT QYeli}
```

**Should be:**
```
Thank you.
```

**Analysis:** "Thank you" completely destroyed

---

#### 9:36-9:39 (Subtitle #38)
**Generated OCR:**
```
AL EICER =11
- You should know.
```

**Should be:**
```
- Where's Earth?
- You should know.
```

**Analysis:** "Where's Earth?" became "AL EICER =11" - apostrophe issue?

---

#### 9:39 (Subtitle #39)
**Generated OCR:**
```
I's not our system.
```

**Should be:**
```
It's not our system.
```

**Analysis:** "It's" became "I's" - classic OCR confusion

---

#### 9:39-9:40 (Subtitle #40)
**Generated OCR:**
```
Yoz 110
```

**Should be:**
```
Scan.
```

**Analysis:** "Scan" became "Yoz 110" - possibly small or stylized text

---

#### 9:54 (Subtitle #43)
**Generated OCR:**
```
Nostromo ouf of the Solomons.
```

**Should be:**
```
Nostromo out of the Solomons.
```

**Analysis:** "out" became "ouf" - t/f confusion

---

#### 10:06-10:08 (Subtitle #48)
**Generated OCR:**
```
N[e]iallg[e}
[ Kane ]
Keep trying.
```

**Should be:**
```
Nothing.
Keep trying.
```

**Analysis:** "Nothing" became "N[e]iallg[e}"

---

#### 10:15-10:17 (Subtitle #46)
**Generated OCR:**
```
AN CEE
the outer rim yet.
That's hard to believe.
```

**Should be:**
```
We haven't reached
the outer rim yet.
That's hard to believe.
```

**Analysis:** "We haven't reached" became "AN CEE"

---

## Error Patterns Identified

### 1. **Complete Line Destruction**
- Many lines turn into complete garbage (special characters, brackets, equals signs)
- Pattern: `[=], (o], \VA, IR, etc.`
- These might be:
  - Special subtitle fonts/styling that Tesseract doesn't recognize
  - Italics or bold text
  - Small text size
  - Overlapping graphics or semi-transparent backgrounds

### 2. **Character Confusion**
- `t` ↔ `f` confusion ("out" → "ouf")
- `It's` → `I's`
- `Radio` → `Radlo` (i/l confusion)
- Number insertion where there shouldn't be any

### 3. **Apostrophe Issues**
- Contractions frequently fail
- "you're" → garbage
- "It's" → "I's"
- "Where's" → garbage

### 4. **Short Words Destroyed**
- "just", "the", "you", "to" frequently become garbage
- Especially at line starts/ends

### 5. **Special Formatting**
- Character names sometimes fail
- Brackets [ ] content (sound effects) sometimes OK, sometimes fail
- Italicized text appears to fail completely

## Recommendations

### 1. **Keep SUP Files for Analysis**
**YES - This is valuable!** Add a toggle to preserve SUP files so we can:
- Visually inspect what was sent to OCR
- Understand font characteristics
- Identify image quality issues
- Debug specific failure cases

### 2. **Pre-processing SUP Images**
Before sending to Tesseract:
- **Contrast Enhancement**: Increase contrast between text and background
- **Binarization**: Convert to pure black text on white background
- **Upscaling**: Scale images 2-3x before OCR (Tesseract works better on larger text)
- **Noise Removal**: Apply denoising filters

### 3. **Tesseract Configuration**
Try different PSM (Page Segmentation Modes):
- Current: Unknown (need to check)
- Try: PSM 7 (single line of text)
- Try: Different OEM modes (LSTM vs Legacy)

### 4. **Post-processing Rules**
Add correction patterns for common mistakes:
```
- "ouf" → "out"
- "I's" → "It's"  
- Line-start "I " → "It " (when followed by 's)
- Remove lines that are >50% special characters
- Flag suspicious patterns: excessive brackets, equals signs, etc.
```

### 5. **Multiple OCR Pass Strategy**
- First pass: Standard Tesseract
- Second pass: If line has >30% garbage characters, retry with:
  - Different preprocessing
  - Different Tesseract config
  - Or mark as [UNCLEAR] for manual review

### 6. **Confidence Scoring**
- Track Tesseract confidence scores per word/line
- Auto-flag low confidence results for review
- Create a "quality report" after extraction

## Implementation Priority

1. **HIGH**: Add SUP file preservation toggle (helps with debugging)
2. **HIGH**: Image upscaling before OCR (proven to help Tesseract)
3. **MEDIUM**: Contrast enhancement preprocessing
4. **MEDIUM**: Post-OCR garbage detection and flagging
5. **LOW**: Multiple pass strategy (more complex)

## Next Steps

1. Extract a few problematic SUP frames from Alien
2. Test different preprocessing techniques
3. Compare Tesseract with different configs
4. Possibly try alternative OCR engines (PaddleOCR, EasyOCR) for comparison

