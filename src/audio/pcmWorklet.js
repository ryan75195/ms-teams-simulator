const FRAME_SAMPLES = 1200;

class PcmDownsampler extends AudioWorkletProcessor {
  constructor() {
    super();
    this.acc = new Int16Array(FRAME_SAMPLES);
    this.accLen = 0;
  }

  process(inputs) {
    const channel = inputs[0]?.[0];
    if (!channel) return true;

    for (let i = 0; i < channel.length; i += 1) {
      const s = Math.max(-1, Math.min(1, channel[i]));
      this.acc[this.accLen] = s < 0 ? s * 0x8000 : s * 0x7fff;
      this.accLen += 1;
      if (this.accLen === FRAME_SAMPLES) {
        const out = this.acc.buffer;
        this.port.postMessage(out, [out]);
        this.acc = new Int16Array(FRAME_SAMPLES);
        this.accLen = 0;
      }
    }
    return true;
  }
}

registerProcessor("pcm-downsampler", PcmDownsampler);
