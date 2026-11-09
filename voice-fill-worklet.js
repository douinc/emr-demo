/* ============================================================
   음성 입력용 AudioWorklet
   마이크 입력(장치 표본화율, mono)을 16 kHz · 16-bit PCM(little-endian)으로 바꿔
   100ms(1,600 표본) 조각마다 메인 스레드로 넘긴다.
   ============================================================ */
class VoiceFillCapture extends AudioWorkletProcessor {
  constructor() {
    super();
    this.targetRate = 16000;
    this.chunk = 1600;                       // 100ms
    this.out = new Int16Array(this.chunk);
    this.filled = 0;
    this.step = sampleRate / this.targetRate; // 입력 표본 몇 개가 출력 표본 하나인가
    this.acc = 0;                             // 지금 출력 표본에 모은 입력 합
    this.accCount = 0;
    this.pos = 0;                             // 다음 출력 경계까지 남은 입력 표본(소수)
    this.peak = 0;
    this.port.onmessage = (e) => { if (e.data === 'flush') this.flush(); };
  }

  push(value) {
    const v = Math.max(-1, Math.min(1, value));
    const a = Math.abs(v);
    if (a > this.peak) this.peak = a;
    this.out[this.filled++] = v < 0 ? v * 0x8000 : v * 0x7fff;
    if (this.filled === this.chunk) this.flush();
  }

  flush() {
    if (!this.filled) return;
    const pcm = this.out.slice(0, this.filled);
    this.port.postMessage({ pcm: pcm.buffer, peak: this.peak }, [pcm.buffer]);
    this.filled = 0;
    this.peak = 0;
  }

  process(inputs) {
    const input = inputs[0];
    if (!input || !input.length) return true;
    const channels = input.length;
    const frames = input[0].length;
    for (let i = 0; i < frames; i++) {
      let sample = 0;
      for (let c = 0; c < channels; c++) sample += input[c][i];
      sample /= channels;
      if (this.step === 1) { this.push(sample); continue; }
      // 내려 표본화: 출력 표본 하나에 해당하는 입력 구간을 평균한다(간단한 저역 통과)
      this.acc += sample;
      this.accCount++;
      this.pos += 1;
      if (this.pos >= this.step) {
        this.push(this.acc / this.accCount);
        this.acc = 0;
        this.accCount = 0;
        this.pos -= this.step;
      }
    }
    return true;
  }
}

registerProcessor('voice-fill-capture', VoiceFillCapture);
