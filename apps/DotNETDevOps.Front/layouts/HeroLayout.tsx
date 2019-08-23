


import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import "@/assets/less/components/hero.less";

import anime from 'animejs';


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


    async mounted() {

        
        console.log("A");
        var tl = anime.timeline({
            easing: 'easeInOutQuad',
            duration: 750,
            autoplay:false
        } as any);

        tl.add({
            targets: "h1.main-h1.home",
            translateY: ["100%", "0%"],
            duration: 300,
            delay: 0,
            easing: 'easeInOutQuad'
        })

        tl.add({
            targets: "h2.main-h2.home",
            translateY: ["-100%", "0%"],
            duration: 300,
            delay: 0,
            easing: 'easeInOutQuad'
        })


        tl.add({
            targets: ".home-subhead",
            translateY: ["100%", "0%"],
            duration: 300,
            delay: 0,
            easing: 'easeInOutQuad'
        })
        if (process.client) {
            await import('waypoints/lib/noframework.waypoints.js');
            var waypoint = new Waypoint({
                element: document.querySelector('.hero'),
                enabled: false,
                handler: function (direction) {
                    console.log(arguments);
                    if (!tl.began)
                        tl.play();
                }
            })
            waypoint.enable();
        }
        setTimeout(() => {
            if (!tl.began)
                tl.play();
        }, 0);


    }

    render() {
        return (
            <div class="hero" style={{ backgroundColor: this.backgroundColor, 'will-change': 'background' }}>
                <img src="https://uploads-ssl.webflow.com/5c0e3c5ca90b66220d8975ec/5c0e3c5ca90b662a7d89762e_arrow.svg" alt="" class="arrow" />
                <div class="wrapper-title" style={{ transform: this.transform, 'will-change': 'transform', 'transform-style': 'preserve-3d'}}>

                    <div class={{ '_w-h1': true, 'last': !this.subtitle }}>
                        <h1  style="transform: translateY(100%)" class="main-h1 home">{this.title}</h1>
                    </div>

                    {
                        this.subtitle ?
                            <div class="_w-h1 last">
                                <h2 class="main-h2 bold home">{this.subtitle}</h2>
                            </div>
                            : null
                    }
                  

                    <div class="home-subhead">
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

            
