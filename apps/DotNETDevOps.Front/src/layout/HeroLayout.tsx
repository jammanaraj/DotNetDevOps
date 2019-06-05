


import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import "@/assets/less/components/hero.less";

export interface HeroLayoutOptions {
    transform: string;
    backgroundColor: string;
    title: string;
    subtitle?: string;
}

@Component
export default class HeroLayout extends tsx.Component<HeroLayoutOptions>{

    @Prop({ default: "rgb(248, 127, 46)" })
    backgroundColor!: string;

    @Prop()
    transform!: string;

    @Prop()
    title!: string;

    @Prop()
    subtitle!: string;

    render() {
        return (
            <div class="hero" style={{ backgroundColor: this.backgroundColor, 'will-change': 'background' }}>
                <img src="https://uploads-ssl.webflow.com/5c0e3c5ca90b66220d8975ec/5c0e3c5ca90b662a7d89762e_arrow.svg" alt="" class="arrow" />
                <div data-w-id="8379a733-d2be-9ef6-7d74-5aa993d72c65" class="wrapper-title" style={{ transform: this.transform }} data-bind="style:{transform:transform,'will-change': 'transform'; 'transform-style': 'preserve-3d'}}">

                    <div class={{ '_w-h1': true, 'last': !this.subtitle }}>
                        <h1  style="transform: translate3d(0px, 0%, 0px) scale3d(1, 1, 1) rotateX(0deg) rotateY(0deg) rotateZ(0deg) skew(0deg, 0deg); color: rgb(0, 0, 0); transform-style: preserve-3d;" class="main-h1 home">{this.title}</h1>
                    </div>

                    {
                        this.subtitle ?
                            <div class="_w-h1 last">
                                <h2 style="transform: translate3d(0px, 0%, 0px) scale3d(1, 1, 1) rotateX(0deg) rotateY(0deg) rotateZ(0deg) skew(0deg, 0deg); color: rgb(0, 0, 0); transform-style: preserve-3d;" class="main-h2 bold home">{this.subtitle}</h2>
                            </div>
                            : null
                    }
                  

                    <div   style="transform: translate3d(0px, 0px, 0px) scale3d(1, 1, 1) rotateX(0deg) rotateY(0deg) rotateZ(0deg) skew(0deg, 0deg); transform-style: preserve-3d;" class="home-subhead">
                        <p class="paragraph-3">
                            Kjeldager Holding IVS
                    <br />
                            Helleborg 15 1.th
                    <br />
                            2700 Brønshøj
                    <br />
                            Denmark
                    <br />
                            CVR: DK40080392
                </p>
                    </div>
                </div>
            </div>

        );
    }
}

            
